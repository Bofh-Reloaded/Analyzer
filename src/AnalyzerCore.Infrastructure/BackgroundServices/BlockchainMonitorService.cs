using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Application.Pools.Commands.CreatePool;
using AnalyzerCore.Domain.Models;
using AnalyzerCore.Domain.Services;
using AnalyzerCore.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace AnalyzerCore.Infrastructure.BackgroundServices
{
    public class BlockchainMonitorService : BackgroundService
    {
        private readonly IBlockchainService _blockchainService;
        private readonly IMediator _mediator;
        private readonly ILogger<BlockchainMonitorService> _logger;
        private readonly ChainConfig _chainConfig;
        private readonly int _pollingInterval;
        private readonly int _blocksToProcess;
        private readonly int _batchSize;
        private readonly int _retryDelay;
        private readonly int _maxRetries;
        private readonly int _requestDelay;
        private readonly AsyncRetryPolicy _retryPolicy;

        public BlockchainMonitorService(
            IBlockchainService blockchainService,
            IMediator mediator,
            ChainConfig chainConfig,
            ILogger<BlockchainMonitorService> logger,
            IConfiguration configuration)
        {
            _blockchainService = blockchainService;
            _mediator = mediator;
            _chainConfig = chainConfig;
            _logger = logger;
            
            var monitoring = configuration.GetSection("Monitoring");
            _pollingInterval = monitoring.GetValue<int>("PollingInterval");
            _blocksToProcess = monitoring.GetValue<int>("BlocksToProcess");
            _batchSize = monitoring.GetValue<int>("BatchSize");
            _retryDelay = monitoring.GetValue<int>("RetryDelay");
            _maxRetries = monitoring.GetValue<int>("MaxRetries");
            _requestDelay = monitoring.GetValue<int>("RequestDelay");

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    _maxRetries,
                    retryAttempt => TimeSpan.FromMilliseconds(_retryDelay * Math.Pow(2, retryAttempt - 1)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            exception,
                            "Error connecting to blockchain (Attempt {RetryCount} of {MaxRetries}). Retrying in {Delay}ms...",
                            retryCount,
                            _maxRetries,
                            timeSpan.TotalMilliseconds);
                    });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Starting blockchain monitor for chain {ChainName}",
                _chainConfig.Name);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var currentBlock = await _blockchainService.GetCurrentBlockNumberAsync(stoppingToken);
                    var fromBlock = currentBlock - BigInteger.Parse(_blocksToProcess.ToString());
                    var toBlock = currentBlock;

                    _logger.LogInformation(
                        "Processing blocks {FromBlock} to {ToBlock} on chain {ChainName}",
                        fromBlock,
                        toBlock,
                        _chainConfig.Name);

                    // Process blocks in batches
                    for (var batchStart = fromBlock; batchStart <= toBlock && !stoppingToken.IsCancellationRequested; batchStart += _batchSize)
                    {
                        var batchEnd = BigInteger.Min(batchStart + _batchSize - 1, toBlock);

                        await _retryPolicy.ExecuteAsync(async () =>
                        {
                            var blocks = await _blockchainService.GetBlocksAsync(batchStart, batchEnd, stoppingToken);
                            foreach (var block in blocks)
                            {
                                if (stoppingToken.IsCancellationRequested) break;

                                foreach (var tx in block.Transactions)
                                {
                                    if (stoppingToken.IsCancellationRequested) break;

                                    if (!string.IsNullOrEmpty(tx.To) && await _blockchainService.IsContractAsync(tx.To, stoppingToken))
                                    {
                                        await ProcessContractInteractionAsync(tx, stoppingToken);
                                    }
                                }
                            }
                        });

                        // Add delay between batches to respect rate limits
                        if (batchEnd < toBlock)
                        {
                            await Task.Delay(_requestDelay, stoppingToken);
                        }
                    }

                    await Task.Delay(_pollingInterval, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        ex,
                        "Error processing blocks on chain {ChainName}. Service will continue...",
                        _chainConfig.Name);
                    
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
            }
        }

        private async Task ProcessContractInteractionAsync(TransactionInfo tx, CancellationToken cancellationToken)
        {
            try
            {
                if (await IsPoolContractAsync(tx.To, cancellationToken))
                {
                    var poolInfo = await _blockchainService.GetPoolInfoAsync(tx.To, cancellationToken);
                    
                    await _mediator.Send(new CreatePoolCommand
                    {
                        Address = tx.To,
                        Token0Address = poolInfo.Token0,
                        Token1Address = poolInfo.Token1,
                        Factory = poolInfo.Factory,
                        Type = poolInfo.Type,
                        ChainId = _chainConfig.ChainId
                    }, cancellationToken);

                    _logger.LogInformation(
                        "Processed new pool: {Address} ({Token0}/{Token1})",
                        tx.To,
                        poolInfo.Token0,
                        poolInfo.Token1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing contract interaction for transaction {Hash}",
                    tx.Hash);
            }
        }

        private async Task<bool> IsPoolContractAsync(string address, CancellationToken cancellationToken)
        {
            try
            {
                await _blockchainService.GetPoolInfoAsync(address, cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
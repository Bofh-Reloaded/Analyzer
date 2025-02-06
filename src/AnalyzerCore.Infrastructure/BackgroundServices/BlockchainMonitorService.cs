using System;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Application.Pools.Commands.CreatePool;
using AnalyzerCore.Application.Pools.Commands.UpdatePoolReserves;
using AnalyzerCore.Domain.Models;
using AnalyzerCore.Domain.Services;
using AnalyzerCore.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

        public BlockchainMonitorService(
            IBlockchainService blockchainService,
            IMediator mediator,
            ChainConfig chainConfig,
            ILogger<BlockchainMonitorService> logger,
            int pollingInterval = 60000, // 1 minute default
            int blocksToProcess = 500)
        {
            _blockchainService = blockchainService;
            _mediator = mediator;
            _chainConfig = chainConfig;
            _logger = logger;
            _pollingInterval = pollingInterval;
            _blocksToProcess = blocksToProcess;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Starting blockchain monitor for chain {ChainName}",
                _chainConfig.Name);

            var lastProcessedBlock = await _blockchainService.GetCurrentBlockNumberAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var currentBlock = await _blockchainService.GetCurrentBlockNumberAsync(stoppingToken);
                    if (currentBlock <= lastProcessedBlock)
                    {
                        _logger.LogWarning(
                            "No new blocks to process on chain {ChainName}. Current: {Current}, Last: {Last}",
                            _chainConfig.Name,
                            currentBlock,
                            lastProcessedBlock);
                        
                        await Task.Delay(_pollingInterval, stoppingToken);
                        continue;
                    }

                    var fromBlock = lastProcessedBlock + 1;
                    var toBlock = Math.Min(lastProcessedBlock + _blocksToProcess, currentBlock);

                    _logger.LogInformation(
                        "Processing blocks {FromBlock} to {ToBlock} on chain {ChainName}",
                        fromBlock,
                        toBlock,
                        _chainConfig.Name);

                    var blocks = await _blockchainService.GetBlocksAsync(fromBlock, toBlock, stoppingToken);
                    foreach (var block in blocks)
                    {
                        foreach (var tx in block.Transactions)
                        {
                            if (await _blockchainService.IsContractAsync(tx.To, stoppingToken))
                            {
                                await ProcessContractInteractionAsync(tx, stoppingToken);
                            }
                        }
                    }

                    lastProcessedBlock = toBlock;
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing blocks on chain {ChainName}",
                        _chainConfig.Name);
                    
                    await Task.Delay(_pollingInterval * 2, stoppingToken); // Back off on error
                }
            }
        }

        private async Task ProcessContractInteractionAsync(TransactionInfo tx, CancellationToken cancellationToken)
        {
            try
            {
                // Check if this is a pool contract
                var isPool = await IsPoolContractAsync(tx.To, cancellationToken);
                if (isPool)
                {
                    var poolInfo = await _blockchainService.GetPoolInfoAsync(tx.To, cancellationToken);
                    
                    // Create or update pool
                    await _mediator.Send(new CreatePoolCommand
                    {
                        Address = tx.To,
                        Token0Address = poolInfo.Token0,
                        Token1Address = poolInfo.Token1,
                        Factory = poolInfo.Factory,
                        Type = poolInfo.Type,
                        ChainId = _chainConfig.ChainId
                    }, cancellationToken);

                    // Update reserves
                    await _mediator.Send(new UpdatePoolReservesCommand
                    {
                        Address = tx.To,
                        ChainId = _chainConfig.ChainId,
                        Reserve0 = poolInfo.Reserve0,
                        Reserve1 = poolInfo.Reserve1
                    }, cancellationToken);
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
                // Try to get pool info - if it succeeds, it's a pool
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
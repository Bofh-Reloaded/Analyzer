using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore.Services
{
    public partial class DataCollectorService : BackgroundService
    {
        // Define the delay between one cycle and another
        private const int TaskDelayMs = 360000;

        private const int NumberOfBlocksToRetrieve = 500;

        private readonly List<string> _addresses;
        private readonly ChainDataHandler _chainDataHandler;
        private readonly string _chainName;

        private readonly Logger _log;

        private readonly int _maxParallelism;

        public readonly Web3 Web3;

        public DataCollectorService(string chainName, string uri, int maxParallelism, ChainDataHandler chainDataHandler,
            List<string> addresses)
        {
            _chainName = chainName;
            _maxParallelism = maxParallelism;
            _chainDataHandler = chainDataHandler;
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            LogContext.PushProperty("SourceContext", $"{_chainName}");
            
            // Registering Web3 client endpoint
            _log.Information($"DataCollectorService Initialized for chain: {chainName}");
            try
            {
                Web3 = new Web3(uri);
            }
            catch
            {
                _log.Error($"Cannot connect to RPC: {uri}.");
            }

            _addresses = addresses;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
                _log.Information($"DataCollectorService background task is stopping for chain: {_chainName}"));
            while (!stoppingToken.IsCancellationRequested)
            {
                _log.Information($"Starting a new cycle for chain: {_chainName}");
                ChainData chainData;
                try
                {
                    chainData = new ChainData(Web3, _chainName, _maxParallelism, _addresses);
                    _log.Information($"Retrieved currentBlock: {chainData.CurrentBlock}");
                }
                catch (Exception)
                {
                    _log.Error("Cannot Connect to RPC Server");
                    await StopAsync(cancellationToken: stoppingToken);
                    return;
                }

                Debug.Assert(chainData != null, nameof(chainData) + " != null");
                var currentBlock = chainData.CurrentBlock;
                _log.Information($"Processing Blocks: {NumberOfBlocksToRetrieve.ToString()}");
                var startBlock = new HexBigInteger(currentBlock.Value - NumberOfBlocksToRetrieve);
                _log.Information($"From Block: {startBlock.Value.ToString()} To Block: {currentBlock.Value.ToString()}");
                await Task.Run(() => 
                    chainData.GetBlocks(
                        (long) startBlock.Value, (long) currentBlock.Value, stoppingToken), 
                    stoppingToken);
                _log.Information($"Total Trx Retrieved: {chainData.Transactions.Count.ToString()}");
                _chainDataHandler.DataChange(chainData);
                await Task.Delay(TaskDelayMs, stoppingToken);
            }
        }
    }
}
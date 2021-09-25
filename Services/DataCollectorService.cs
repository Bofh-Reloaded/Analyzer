using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
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
        private const int TASK_TASK_DELAY_MS = 360000;

        private const int TASK_NUMBER_OF_BLOCKS_TO_RETRIEVE = 500;

        private readonly List<string> _addresses;
        private readonly ChainDataHandler _chainDataHandler;
        private readonly string _chainName;

        private readonly Logger _log;

        private readonly int _maxParallelism;

        private readonly Web3 _web3;

        public DataCollectorService(AnalyzerConfig config, ChainDataHandler chainDataHandler)
        {
            _chainName = config.ChainName;
            _maxParallelism = config.ServicesConfig.MaxParallelism;
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
            _log.Information($"DataCollectorService Initialized for chain: {config.ChainName}");
            try
            {
                _web3 = new Web3($"http://{config.RpcEndpoints.First()}:{config.RpcPort}");
            }
            catch
            {
                _log.Error($"Cannot connect to RPC: {config.RpcEndpoints.First()}.");
            }

            _addresses = config.Enemies;
            _addresses.Add(config.Address);
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
                    chainData = new ChainData(_web3, _chainName, _maxParallelism, _addresses);
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
                _log.Information($"Processing Blocks: {TASK_NUMBER_OF_BLOCKS_TO_RETRIEVE.ToString()}");
                var startBlock = new HexBigInteger(currentBlock.Value - TASK_NUMBER_OF_BLOCKS_TO_RETRIEVE);
                _log.Information($"From Block: {startBlock.Value.ToString()} To Block: {currentBlock.Value.ToString()}");
                await Task.Run(() => 
                    chainData.GetBlocks(
                        (long) startBlock.Value, (long) currentBlock.Value, stoppingToken), 
                    stoppingToken);
                _log.Information($"Total Trx Retrieved: {chainData.Transactions.Count.ToString()}");
                _chainDataHandler.DataChange(chainData);
                await Task.Delay(TASK_TASK_DELAY_MS, stoppingToken);
            }
        }
    }
}
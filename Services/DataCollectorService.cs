using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
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
        private readonly string _version;

        private readonly Web3 _web3;
        private HexBigInteger _oldBlock = new HexBigInteger(new BigInteger(0));

        public DataCollectorService(AnalyzerConfig config, ChainDataHandler chainDataHandler, string version)
        {
            _chainName = config.ChainName;
            _maxParallelism = config.ServicesConfig.MaxParallelism;
            _chainDataHandler = chainDataHandler;
            _version = version;
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
            _log.Information("DataCollectorService Initialized for chain: {ChainName}", config.ChainName);
            try
            {
                _web3 = new Web3($"http://{config.RpcEndpoints.First()}:{config.RpcPort.ToString()}");
            }
            catch
            {
                _log.Error("Cannot connect to RPC: {RpcEndPoint}", config.RpcEndpoints.First());
            }

            _addresses = config.Enemies;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
                _log.Information("DataCollectorService background task is stopping for chain: {ChainName}",
                    _chainName));
            while (!stoppingToken.IsCancellationRequested)
            {
                _log.Information("Starting a new cycle for chain: {ChainName}", _chainName);
                ChainData chainData;
                try
                {
                    chainData = new ChainData(_web3, _chainName, _maxParallelism, _addresses);
                    _log.Information("Retrieved currentBlock: {CurrentBlock}", chainData.CurrentBlock);
                }
                catch (Exception)
                {
                    _log.Error("Cannot Connect to RPC Server");
                    await StopAsync(cancellationToken: stoppingToken);
                    return;
                }

                Debug.Assert(chainData != null, nameof(chainData) + " != null");
                var currentBlock = chainData.CurrentBlock;
                if (_oldBlock.Value != 0)
                {
                    if (_oldBlock.Value == currentBlock.Value)
                    {
                        _log.Error("We are retrieving the same block, change RPC. Stopping Service");
                        await StopAsync(stoppingToken);
                    }
                }
                _oldBlock = currentBlock;
                _log.Information("Processing Blocks: {Block}", TASK_NUMBER_OF_BLOCKS_TO_RETRIEVE.ToString());
                var startBlock = new HexBigInteger(currentBlock.Value - TASK_NUMBER_OF_BLOCKS_TO_RETRIEVE);
                _log.Information("From Block: {StartBlock} To Block: {EndBlock}",
                    startBlock.Value.ToString(),
                    currentBlock.Value.ToString());
                await Task.Run(() => 
                    chainData.GetBlocks(
                        (long) startBlock.Value, (long) currentBlock.Value, stoppingToken), 
                    stoppingToken);
                _log.Information("Total Trx Retrieved: {TransactionCount}", chainData.Transactions.Count.ToString());
                _chainDataHandler.DataChange(chainData);
                await Task.Delay(TASK_TASK_DELAY_MS, stoppingToken);
            }
        }
    }
}
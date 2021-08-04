using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;

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

        private readonly ILog _log;

        private readonly int _maxParallelism;

        public readonly Web3 Web3;

        public DataCollectorService(string chainName, string uri, int maxParallelism, ChainDataHandler chainDataHandler,
            List<string> addresses)
        {
            _chainName = chainName;
            _maxParallelism = maxParallelism;
            _chainDataHandler = chainDataHandler;
            _log = LogManager.GetLogger($"{MethodBase.GetCurrentMethod()?.DeclaringType}: {this._chainName}");
            // Registering Web3 client endpoint
            _log.Info($"DataCollectorService Initialized for chain: {chainName}");
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
                _log.Info($"DataCollectorService background task is stopping for chain: {_chainName}"));
            while (!stoppingToken.IsCancellationRequested)
            {
                _log.Info($"Starting a new cycle for chain: {_chainName}");
                ChainData chainData = null;
                try
                {
                    chainData = new ChainData(Web3, _chainName, _maxParallelism, _addresses);
                    _log.Info($"Retrieved currentBlock: {chainData.CurrentBlock}");
                }
                catch (Exception)
                {
                    _log.Error("Cannot Connect to RPC Server");
                    await StopAsync(cancellationToken: stoppingToken);
                    return;
                }

                Debug.Assert(chainData != null, nameof(chainData) + " != null");
                var currentBlock = chainData.CurrentBlock;
                _log.Info($"Processing Blocks: {NumberOfBlocksToRetrieve.ToString()}");
                var startBlock = new HexBigInteger(currentBlock.Value - NumberOfBlocksToRetrieve);
                _log.Info($"From Block: {startBlock.Value.ToString()} To Block: {currentBlock.Value.ToString()}");
                await Task.Run(() => chainData.GetBlocks((long) startBlock.Value, (long) currentBlock.Value, stoppingToken), stoppingToken);
                _log.Info($"Total Trx Retrieved: {chainData.Transactions.Count.ToString()}");
                _chainDataHandler.DataChange(chainData);
                await Task.Delay(TaskDelayMs, stoppingToken);
            }
        }
    }
}
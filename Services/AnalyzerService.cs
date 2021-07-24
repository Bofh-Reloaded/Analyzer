using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using log4net;
using Nethereum.RPC.Eth.DTOs;

namespace AnalyzerCore.Services
{
    public sealed class AnalyzerService : BackgroundService, IObserver<DataCollectorService.ChainData>
    {
        // Define the delay between one cycle and another
        private const int TaskDelayMs = 360000;

        // Array of block to analyze
        private static readonly List<int> NumbersOfBlocksToAnalyze = new() {25, 100, 500};

        // Initialize an empty list of string that will be filled with addresses
        private readonly List<string> _addresses;

        private readonly int _blockDurationTime;
        private readonly DataCollectorService.ChainDataHandler _chainDataHandler;

        // String Value representing the chain name
        private readonly string _chainName;

        // Initialize Logger
        private readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );

        private readonly string _ourAddress;

        // Define the TelegramNotifier Instance
        private readonly TelegramNotifier _telegramNotifier;
        private IDisposable _cancellation;

        public AnalyzerService(string chainName, List<string> addresses, TelegramNotifier telegramNotifier,
            int blockDurationTime, string ourAddress, DataCollectorService.ChainDataHandler chainDataHandler)
        {
            if (blockDurationTime <= 0) throw new ArgumentOutOfRangeException(nameof(blockDurationTime));
            // Filling instance variable
            _chainName = chainName ?? throw new ArgumentNullException(nameof(chainName));
            _addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
            _telegramNotifier = telegramNotifier ?? throw new ArgumentNullException(nameof(telegramNotifier));
            _blockDurationTime = blockDurationTime;
            _ourAddress = ourAddress ?? throw new ArgumentNullException(nameof(ourAddress));
            _addresses.Add(ourAddress);
            _chainDataHandler = chainDataHandler;

            _log.Info($"AnalyzerService Initialized for chain: {chainName}");
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(DataCollectorService.ChainData chainData)
        {
            _log.Info("New Data Received");
            if (chainData == null) return;
            if (chainData.Transactions.Count <= 0) return;
            
            var msg = new Message
            {
                Addresses = new List<AddressStats>(),
                Timestamp =
                    $"<b>\U0001F550[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}]\U0001F550</b>"
            };

            _log.Info($"Total trx: {chainData.Transactions.Count.ToString()}");

            /* Checking succeded transactions */
            foreach (var address in _addresses)
            {
                var addsStats = new AddressStats {Address = address, BlockRanges = new List<BlockRangeStats>()};
                // Init Transactions Data
                chainData.GetAddressTransactions(address);

                _log.Info(
                    $"Evaluating Address: {address} with trx amount: {chainData.Addresses[address].Transactions.Count.ToString()}");

                foreach (var numberOfBlocks in NumbersOfBlocksToAnalyze.OrderBy(i => i))
                {
                    _log.Info(
                        // ReSharper disable once HeapView.BoxingAllocation
                        $"NB: {numberOfBlocks.ToString()} Evaluating SB: {chainData.CurrentBlock.Value - numberOfBlocks} TB: {chainData.CurrentBlock.Value.ToString()}");

                    var trxToAnalyze =
                        chainData.Addresses[address]
                            .Transactions.Where(t =>
                                t.Transaction.BlockNumber >= chainData.CurrentBlock.Value - numberOfBlocks)
                            .ToList();
                    var succededTrxs = trxToAnalyze
                        .Where(
                            t => t.TransactionReceipt.Succeeded() && t.TransactionReceipt.Logs.Count > 0)
                        .ToList();
                    _log.Info($"TRX to analyze: {trxToAnalyze.Count().ToString()}");

                    try
                    {
                        // Calculate the success rate and construct che BlockRangeStat object
                        long successRate = 100 * succededTrxs.Count / trxToAnalyze.Count();
                        var blockRangeStats = new BlockRangeStats
                        {
                            BlockRange = numberOfBlocks,
                            SuccededTranstactionsPerBlockRange = succededTrxs.Count,
                            TotalTransactionsPerBlockRange = trxToAnalyze.Count,
                            SuccessRate = $"{successRate.ToString()}%"
                        };
                        if (string.Equals(address.ToLower(), _ourAddress.ToLower(), StringComparison.Ordinal))
                        {
                            // Analyze the stats for type of trade
                            blockRangeStats.T0Trx = trxToAnalyze
                                .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T0}"))
                                .ToList();
                            blockRangeStats.T1Trx = trxToAnalyze
                                .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T1}"))
                                .ToList();
                            blockRangeStats.T2Trx = trxToAnalyze
                                .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T2}"))
                                .ToList();
                            blockRangeStats.ContP = trxToAnalyze.Where(t =>
                                    t.Transaction.Input.StartsWith($"0x{OpCodes.Cont}"))
                                .ToList();
                            blockRangeStats.T0TrxSucceded =
                                succededTrxs.Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T0}"))
                                    .ToList();
                            blockRangeStats.T1TrxSucceded =
                                succededTrxs.Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T1}"))
                                    .ToList();
                            blockRangeStats.T2TrxSucceded =
                                succededTrxs.Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T2}"))
                                    .ToList();
                            blockRangeStats.ContPSucceded = succededTrxs
                                .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.Cont}"))
                                .ToList();
                        }

                        addsStats.BlockRanges.Add(blockRangeStats);
                    }
                    catch (DivideByZeroException)
                    {
                        _log.Error("No transaction retrieved");
                    }
                }

                msg.Addresses.Add(addsStats);
            }

            msg.TotalTrx = chainData.Transactions.Count;
            msg.TPS = chainData.Transactions.Count / _blockDurationTime;

            msg.ourAddress = _ourAddress;

            _telegramNotifier.SendStatsRecap(msg);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Info($"Starting AnalyzerService for chain: {_chainName}");
            _telegramNotifier.SendMessage($"Starting AnalyzerService for chain: {_chainName}");
            stoppingToken.Register(() =>
                {
                    Unsubscribe();
                    _log.Info($"AnalyzerService background task is stopping for chain: {_chainName}");
                }
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                Subscribe(_chainDataHandler);
                await Task.Delay(TaskDelayMs, stoppingToken);
            }
        }

        private void Subscribe(DataCollectorService.ChainDataHandler provider)
        {
            _cancellation = provider.Subscribe(this);
        }

        private void Unsubscribe()
        {
            _cancellation.Dispose();
        }
    }
}
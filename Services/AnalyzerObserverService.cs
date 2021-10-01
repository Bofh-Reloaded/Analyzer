using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using Nethereum.RPC.Eth.DTOs;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore.Services
{
    public sealed class AnalyzerService : BackgroundService, IObserver<DataCollectorService.ChainData>
    {
        // Define the delay between one cycle and another
        private const int TASK_TASK_DELAY_MS = 360000;

        // Array of block to analyze
        private static readonly List<int> NumbersOfBlocksToAnalyze = new List<int>() { 25, 100, 500 };

        // Initialize an empty list of string that will be filled with addresses
        private readonly List<string> _addresses;

        private readonly int _blockDurationTime;
        private readonly DataCollectorService.ChainDataHandler _chainDataHandler;
        private readonly string _version;

        // String Value representing the chain name
        private readonly string _chainName;

        // Initialize Logger
        private readonly Logger _log;

        private readonly string _ourAddress;

        // Define the TelegramNotifier Instance
        private readonly TelegramNotifier _telegramNotifier;
        private IDisposable _cancellation;

        public AnalyzerService(AnalyzerConfig config, DataCollectorService.ChainDataHandler chainDataHandler, string version)
        {
            if (config.ServicesConfig.AnalyzerService.BlockDurationTime <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.ServicesConfig.AnalyzerService.BlockDurationTime));
            // Filling instance variable
            _chainName = config.ChainName;
            _addresses = config.Enemies;
            _addresses.Add(config.Address);
            _telegramNotifier = new TelegramNotifier(config.ServicesConfig.AnalyzerService.ChatId,
                config.ServicesConfig.AnalyzerService.BotToken);
            _blockDurationTime = config.ServicesConfig.AnalyzerService.BlockDurationTime;
            _ourAddress = config.Address;
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

            _log.Information($"AnalyzerService Initialized for chain: {config.ChainName}");
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
            _log.Information("New Data Received");
            if (chainData == null) return;
            if (chainData.Transactions.Count <= 0) return;

            _log.Information($"Total trx: {chainData.Transactions.Count.ToString()}");

            var msg = new Message
            {
                Addresses = new List<AddressStats>(),
                Timestamp =
                    $"<b>\U0001F550[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}]\U0001F550 To Current Block: {chainData.CurrentBlock}</b>"
            };

            /* Checking succeded transactions */
            foreach (var address in _addresses)
            {
                var addsStats = new AddressStats { Address = address, BlockRanges = new List<BlockRangeStats>() };
                // Init Transactions Data
                chainData.GetAddressTransactions(address);

                _log.Information(
                    $"Evaluating Address: {address} with trx amount: {chainData.Addresses[address].Transactions.Count.ToString()}");

                foreach (var numberOfBlocks in NumbersOfBlocksToAnalyze.OrderBy(i => i))
                {
                    _log.Information(
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
                    _log.Information($"TRX to analyze: {trxToAnalyze.Count().ToString()}");

                    // Calculate the success rate and construct che BlockRangeStat object
                    var blockRangeStats = new BlockRangeStats
                    {
                        BlockRange = numberOfBlocks,
                        SuccededTranstactionsPerBlockRange = succededTrxs.Count,
                        TotalTransactionsPerBlockRange = trxToAnalyze.Count,
                        SuccessRate = succededTrxs.Count > 0
                            ? $"{100 * succededTrxs.Count / trxToAnalyze.Count}%"
                            : "0"
                    };
                    if (string.Equals(address.ToLower(), _ourAddress.ToLower(), StringComparison.Ordinal))
                    {
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
                        blockRangeStats.Unknown = trxToAnalyze.Where(t =>
                            t.Transaction.Input.StartsWith($"0x{OpCodes.Unknown}")).ToList();
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
                        blockRangeStats.UnknownSucceded = succededTrxs
                            .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.Unknown}"))
                            .ToList();
                    }

                    addsStats.BlockRanges.Add(blockRangeStats);
                }

                msg.Addresses.Add(addsStats);
            }

            msg.TotalTrx = chainData.Transactions.Count;
            msg.Tps = chainData.Transactions.Count / _blockDurationTime;

            msg.OurAddress = _ourAddress;

            _telegramNotifier.SendStatsRecap(msg);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information("Starting AnalyzerService for chain: {ChainName}, version: {Version}", _chainName, _version);
            _telegramNotifier.SendMessage(string.Format("Starting AnalyzerService for chain: {0}, version: {1}", _chainName, _version));
            stoppingToken.Register(() =>
                {
                    Unsubscribe();
                    _log.Information($"AnalyzerService background task is stopping for chain: {_chainName}");
                }
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                Subscribe(_chainDataHandler);
                await Task.Delay(TASK_TASK_DELAY_MS, stoppingToken);
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
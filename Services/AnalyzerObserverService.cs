using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
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
        private const int TASK_TASK_TASK_DELAY_MS = 360000;

        // Array of block to analyze
        private static readonly List<int> NumbersOfBlocksToAnalyze = new() { 25, 100, 500 };

        // Initialize an empty list of string that will be filled with addresses
        private readonly List<string> _addresses;

        private readonly int _blockDurationTime;
        private readonly DataCollectorService.ChainDataHandler _chainDataHandler;

        // String Value representing the chain name
        private readonly string _chainName;

        // Initialize Logger
        private readonly Logger _log;

        private readonly List<string> _ourAddresses;

        // Define the TelegramNotifier Instance
        private readonly TelegramNotifier _telegramNotifier;
        private readonly string _version;
        private IDisposable _cancellation;
        private DataCollectorService.ChainData _chainData;

        public AnalyzerService(AnalyzerConfig config, DataCollectorService.ChainDataHandler chainDataHandler,
            string version)
        {
            if (config.ServicesConfig.AnalyzerService.BlockDurationTime <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.ServicesConfig.AnalyzerService.BlockDurationTime));
            // Filling instance variable
            _chainName = config.ChainName;
            _addresses = config.Enemies;
            _telegramNotifier = new TelegramNotifier(config.ServicesConfig.AnalyzerService.ChatId,
                config.ServicesConfig.AnalyzerService.BotToken, config);
            _blockDurationTime = config.ServicesConfig.AnalyzerService.BlockDurationTime;
            _ourAddresses = config.Wallets;
            _chainDataHandler = chainDataHandler;
            _version = version;
            
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    LogEventLevel.Debug,
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            LogContext.PushProperty("SourceContext", $"{_chainName}");

            _log.Information("AnalyzerService Initialized for chain: {StringChainName}", config.ChainName);
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        private async Task<BlockRangeStats> CreateBlockRange(int numberOfBlocks,
            DataCollectorService.ChainData chainData,
            string address)
        {
            _log.Information("NB: {NumberOfBlocks} Evaluating SB: {AmountOfBlocks} TB: {CurrentBlock}"
                ,
                numberOfBlocks.ToString(),
                (chainData.CurrentBlock.Value - numberOfBlocks),
                chainData.CurrentBlock.Value.ToString());
            var trxToAnalyze =
                chainData.Addresses[address]
                    .Transactions.Where(t =>
                        t.Transaction.BlockNumber >= chainData.CurrentBlock.Value - numberOfBlocks)
                    .ToList();
            var succededTrxs = trxToAnalyze
                .Where(
                    t => t.TransactionReceipt.Succeeded() && t.TransactionReceipt.Logs.Count > 0)
                .ToList();
            _log.Information("TRX to analyze: {NumberOfTransactionsToAnalyze}",
                trxToAnalyze.Count().ToString());

            // Calculate the success rate and construct che BlockRangeStat object
            return new BlockRangeStats
            {
                BlockRange = numberOfBlocks,
                SuccededTranstactionsPerBlockRange = succededTrxs.Count,
                TotalTransactionsPerBlockRange = trxToAnalyze.Count,
                SuccessRate = succededTrxs.Count > 0
                    ? $"{100 * succededTrxs.Count / trxToAnalyze.Count}%"
                    : "0"
            };
        }

        public async void OnNext(DataCollectorService.ChainData chainData)
        {
            _log.Information("New Data Received");
            if (chainData == null) return;
            if (chainData.Transactions.Count <= 0) return;

            _log.Information("Total trx: {NumberOfTransaction}", chainData.Transactions.Count.ToString());

            // Propagating _chainData
            _chainData = chainData;
            
            // Build and delivery stat message
            // await ReportCompetitor();
            var msg = await CreateOurAddressesReport();
            _telegramNotifier.SendOurStatsRecap(msg);
        }

        private async Task<Message> CreateOurAddressesReport()
        {
            var telegramMessage = new Message
            {
                Addresses = new List<AddressStats>(),
                Timestamp =
                    $"<b>\U0001F550[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}]\U0001F550 To Current Block: {_chainData.CurrentBlock}</b>"
            };
            _log.Debug("OurAddresses: {OurAddresses}", JsonSerializer.Serialize(_ourAddresses));
            foreach (var wallet in _ourAddresses)
            {
                var addsStats = new AddressStats { Address = wallet, BlockRanges = new List<BlockRangeStats>() };
                _chainData.GetAddressTransactions(wallet);
                foreach (var numberOfBlocks in NumbersOfBlocksToAnalyze.OrderBy(i => i))
                {
                    var trxToAnalyze =
                        _chainData.Addresses[wallet]
                            .Transactions.Where(t =>
                                t.Transaction.BlockNumber >= _chainData.CurrentBlock.Value - numberOfBlocks)
                            .ToList();
                    var succededTrxs = trxToAnalyze
                        .Where(
                            t => t.TransactionReceipt.Succeeded() && t.TransactionReceipt.Logs.Count > 0)
                        .ToList();
                    var blockRangeStats = new BlockRangeStats
                    {
                        BlockRange = numberOfBlocks,
                        SuccededTranstactionsPerBlockRange = succededTrxs.Count,
                        TotalTransactionsPerBlockRange = trxToAnalyze.Count,
                        SuccessRate = succededTrxs.Count > 0
                            ? $"{100 * succededTrxs.Count / trxToAnalyze.Count}%"
                            : "0",
                        // Compute our transactions
                        T0Trx = trxToAnalyze
                            .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T0}"))
                            .ToList(),
                        T1Trx = trxToAnalyze
                            .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T1}"))
                            .ToList(),
                        T2Trx = trxToAnalyze
                            .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T2}"))
                            .ToList(),
                        ContP = trxToAnalyze.Where(t =>
                                t.Transaction.Input.StartsWith($"0x{OpCodes.Cont}"))
                            .ToList(),
                        Unknown = trxToAnalyze.Where(t =>
                            t.Transaction.Input.StartsWith($"0x{OpCodes.Unknown}")).ToList(),
                        T0TrxSucceded = succededTrxs.Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T0}"))
                            .ToList(),
                        T1TrxSucceded = succededTrxs.Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T1}"))
                            .ToList(),
                        T2TrxSucceded = succededTrxs.Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.T2}"))
                            .ToList(),
                        ContPSucceded = succededTrxs
                            .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.Cont}"))
                            .ToList(),
                        UnknownSucceded = succededTrxs
                            .Where(t => t.Transaction.Input.StartsWith($"0x{OpCodes.Unknown}"))
                            .ToList()
                    };
                    _log.Debug("adding blockRangeStats for address {Address} with block range {BlockRange}",
                        addsStats.Address,
                        blockRangeStats.BlockRange);
                    addsStats.BlockRanges.Add(blockRangeStats);
                }

                telegramMessage.Addresses.Add(addsStats);

                telegramMessage.TotalTrx = _chainData.Transactions.Count;
                telegramMessage.Tps = _chainData.Transactions.Count / _blockDurationTime;
            }
            return telegramMessage;
        }

        private async Task ReportCompetitor()
        {
            var msg = new Message
            {
                Addresses = new List<AddressStats>(),
                Timestamp =
                    $"<b>\U0001F550[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}]\U0001F550 To Current Block: {_chainData.CurrentBlock}</b>"
            };

            /* Checking succeded transactions */
            foreach (var address in _addresses)
            {
                var addsStats = new AddressStats { Address = address, BlockRanges = new List<BlockRangeStats>() };
                // Init Transactions Data
                _chainData.GetAddressTransactions(address);

                _log.Information("Evaluating Address:{Address} with trx amount: {NumberOfTransactions}",
                    address,
                    _chainData.Addresses[address]
                        .Transactions.Count.ToString());

                foreach (var numberOfBlocks in NumbersOfBlocksToAnalyze.OrderBy(i => i))
                {
                    addsStats.BlockRanges.Add(await CreateBlockRange(numberOfBlocks, _chainData, address));
                }

                msg.Addresses.Add(addsStats);
            }

            msg.TotalTrx = _chainData.Transactions.Count;
            msg.Tps = _chainData.Transactions.Count / _blockDurationTime;

            _telegramNotifier.SendStatsRecap(msg);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information("Starting AnalyzerService for chain: {ChainName}, version: {Version}",
                _chainName,
                _version);
            // _telegramNotifier.SendMessage($"Starting AnalyzerService for chain: {_chainName}, version: {_version}");
            stoppingToken.Register(() =>
                {
                    Unsubscribe();
                    _log.Information("AnalyzerService background task is stopping for chain: {StringChainName}",
                        _chainName);
                }
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                Subscribe(_chainDataHandler);
                await Task.Delay(TASK_TASK_TASK_DELAY_MS, stoppingToken);
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
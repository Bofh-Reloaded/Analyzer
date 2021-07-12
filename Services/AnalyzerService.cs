using System;
using log4net;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Configuration;
using AnalyzerCore.Notifier;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using System.Numerics;
using AnalyzerCore.Models;
using System.Collections.Concurrent;
using System.Globalization;
using Nethereum.RPC.Eth.DTOs;
using System.IO;
using Newtonsoft.Json.Linq;

namespace AnalyzerCore.Services
{

    public class Options
    {
        public List<string> addresses { get; set; }
        public string ourAddress { get; set; }
    }

    public class AnalyzerService : BackgroundService
    {
        // Initialize Logger
        private readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
            );

        // Define the delay between one cycle and another
        private readonly int _taskDelayMs = 360000;

        // Array of block to analyze
        private static readonly List<int> NumbersOfBlocksToAnalyze = new List<int> { 25, 100, 500 };

        // Define the TelegramNotifier Instance
        private readonly TelegramNotifier _telegramNotifier;

        private int blockDurationTime { get; set; }
        private int maxParallelism { get; set; }
        private string ourAddress { get; set; }

        // Define Web3 (Nethereum) client
        private readonly Web3 _web3;

        // String Value representing the chain name
        private readonly string _chainName;

        // Initiliaze configuration accessor
        public IConfigurationRoot Configuration;

        // Inizialize an empty list of string that will be filled with addresses
        private readonly List<string> _addresses;

        private const string TokenAddressToCompareWith = "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178";
        private const string SyncEventAddress = "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        private static class OpCodes
        {
            public static readonly string T0 = new string("085ea7b3");
            public static readonly string T1 = new string("185ea7b3");
            public static readonly string T2 = new string("285ea7b3");
            public static readonly string Cont = new string("985ea7b3");
        }

        public AnalyzerService(string chainName, string uri, List<string> addresses, TelegramNotifier telegramNotifier,
            int blockDurationTime, int maxParallelism, string ourAddress)
        {
            if (blockDurationTime <= 0) throw new ArgumentOutOfRangeException(nameof(blockDurationTime));
            if (maxParallelism <= 0) throw new ArgumentOutOfRangeException(nameof(maxParallelism));
            // Filling instance variable
            this._chainName = chainName ?? throw new ArgumentNullException(nameof(chainName));
            this._addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
            this._telegramNotifier = telegramNotifier ?? throw new ArgumentNullException(nameof(telegramNotifier));
            this.blockDurationTime = blockDurationTime;
            this.maxParallelism = maxParallelism;
            this.ourAddress = ourAddress ?? throw new ArgumentNullException(nameof(ourAddress));
            this._addresses.Add(ourAddress);

            // Load configuration regarding tokens
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("tokens.json", false, reloadOnChange: true)
                .Build();
            var section = configuration.Get<TokenListConfig>();

            // Registering Nethereum Web3 client endpoint
            _log.Info($"AnalyzerService Initialized for chain: {chainName}");
            _web3 = new Web3(uri);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Info($"Starting AnalyzerService for chain: {_chainName}");
            _telegramNotifier.SendMessage($"Starting AnalyzerService for chain: {_chainName}");
            stoppingToken.Register(() =>
                _log.Info($"AnalyzerService background task is stopping for chain: {_chainName}"));

            while (!stoppingToken.IsCancellationRequested)
            {
                _log.Info("New Analsys Cycle");

                var msg = new Message();
                msg.Addresses = new List<AddressStats>();
                msg.Timestamp = $"<b>\U0001F550[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}]\U0001F550</b>";

                // Get Current Block
                HexBigInteger currentBlock = null;
                try {
                    currentBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                    _log.Info(currentBlock.Value.ToString());
                } catch (Nethereum.JsonRpc.Client.RpcClientTimeoutException)
                {
                    _log.Error($"Cannot connect to RPC for chain: {_chainName}");
                    await Task.Delay(10000, stoppingToken);
                }

                if (currentBlock is { })
                {
                    HexBigInteger startBlock = new HexBigInteger(
                        (BigInteger)currentBlock.Value - (BigInteger)NumbersOfBlocksToAnalyze.Max()
                    );

                    // Get all the transactions inside the blocks between latest and latest - 500
                    BlockingCollection<Transaction> trx = GetBlocksAsync(startBlock: startBlock, currentBlock: currentBlock);
                    _log.Info($"Total trx: {trx.Count.ToString()}");

                    /* Checking succeded transactions */
                    foreach (var address in _addresses)
                    {

                        var addrStats = new AddressStats {Address = address, BlockRanges = new List<BlockRangeStats>()};

                        BlockingCollection<Transaction> addrTrxs = new BlockingCollection<Transaction>();
                        Parallel.ForEach(trx, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, tr =>
                        {
                            // Skip if To field is empty
                            if (tr.To != null && tr.From != null)
                            {
                                // Check if the address is inside From field
                                if (tr.From.ToLower() == address.ToLower())
                                {
                                    addrTrxs.Add(tr);
                                    return;
                                } 

                                // Otherwise check if the address is inside the To field (meaning that we are working with a contract
                                if (tr.To.ToLower() == address.ToLower())
                                {
                                    addrTrxs.Add(tr);
                                    return;
                                }
                            }
                        });
                        _log.Info($"Evaluating Address: {address} with trx amount: {addrTrxs.Count.ToString()}");
                        foreach (var numberOfBlocks in NumbersOfBlocksToAnalyze.OrderBy(i => i))
                        {
                            _log.Info(
                                $"NB: {numberOfBlocks.ToString()} Evaluating SB: {currentBlock.Value - numberOfBlocks)} TB: {currentBlock.Value.ToString()}");

                            BlockingCollection<Transaction> succededTrxs = new BlockingCollection<Transaction>();
                            var trxToAnalyze = addrTrxs.Where(t => t.BlockNumber >= currentBlock.Value - numberOfBlocks);
                            _log.Info($"TRX to analyze: {trxToAnalyze.Count().ToString()}");
                            Parallel.ForEach(trxToAnalyze, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, _t =>
                            {
                                _log.Debug($"Getting Receipt for trx hash: {_t.TransactionHash}");

                                // Initialize receipt variable
                                Task<TransactionReceipt> receipt = null;

                                // Try to get the transaction receipt
                                try
                                {
                                    receipt = _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(_t.TransactionHash);
                                    receipt.Wait();
                                }
                                catch (Exception e)
                                {
                                    _log.Error(e.ToString());
                                }

                                if (!(receipt is {Result: { }})) return;
                                if (!receipt.Result.Status.Value.IsOne || receipt.Result.Logs.Count <= 0) return;
                                    _log.Debug($"Succeeded trx with hash: {_t.TransactionHash}");
                                    succededTrxs.Add(_t, stoppingToken);
                                    // Analyze Tokens
                                    if (!string.Equals(address, TokenAddressToCompareWith,
                                        StringComparison.CurrentCultureIgnoreCase)) return;
                                    {
                                        var logsList = receipt.Result.Logs.ToList();
                                        var syncEvents = logsList.Where(
                                            e => string.Equals(e["topics"][key: 0.ToString()].ToString().ToLower(),
                                                SyncEventAddress, StringComparison.Ordinal)
                                        ).ToList();
                                        foreach (var contract in syncEvents)
                                        {
                                            var contractAddress = contract["address"].ToString();
                                            var contractDetail = _web3.Eth.GetContract(abi: "", contractAddress: contractAddress);

                                        }
                                        _log.Info(Dump(syncEvents));
                                    }
                            });
                            try
                            {
                                // Calculate the success rate and construct che BlockRangeStat object
                                long successRate = 100 * succededTrxs.Count / trxToAnalyze.Count();
                                var blockRangeStats = new BlockRangeStats
                                {
                                    BlockRange = numberOfBlocks,
                                    SuccededTranstactionsPerBlockRange = succededTrxs.Count,
                                    TotalTransactionsPerBlockRange = trxToAnalyze.Count(),
                                    SuccessRate = $"{successRate.ToString()}%"
                                };
                                if (string.Equals(address.ToLower(), ourAddress.ToLower(), StringComparison.Ordinal))
                                {
                                    // Analyze the stats for type of trade
                                    blockRangeStats.T0Trx = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.T0}") == true).ToList();
                                    blockRangeStats.T1Trx = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.T1}") == true).ToList();
                                    blockRangeStats.T2Trx = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.T2}") == true).ToList();
                                    blockRangeStats.ContP = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.Cont}") == true).ToList();
                                    blockRangeStats.T0TrxSucceded = succededTrxs.Where(t => t.Input.StartsWith($"0x{OpCodes.T0}") == true).ToList();
                                    blockRangeStats.T1TrxSucceded = succededTrxs.Where(t => t.Input.StartsWith($"0x{OpCodes.T1}") == true).ToList();
                                    blockRangeStats.T2TrxSucceded = succededTrxs.Where(t => t.Input.StartsWith($"0x{OpCodes.T2}") == true).ToList();
                                    blockRangeStats.ContPSucceded = succededTrxs.Where(t => t.Input.StartsWith($"0x{OpCodes.Cont}") == true).ToList();

                                }
                                addrStats.BlockRanges.Add(blockRangeStats);
                            }
                            catch (System.DivideByZeroException)
                            {
                                _log.Error("No transaction retrieved");
                                continue;
                            }
                        }
                        msg.Addresses.Add(addrStats);
                    }

                    msg.TotalTrx = trx.Count;
                    msg.TPS = trx.Count / blockDurationTime;
                }

                msg.ourAddress = ourAddress;

                _telegramNotifier.SendStatsRecap(message: msg);

                await Task.Delay(_taskDelayMs, stoppingToken);
            }
        }

        private BlockingCollection<Transaction> GetBlocksAsync(HexBigInteger startBlock, HexBigInteger currentBlock)
        {
            var  trx = new BlockingCollection<Transaction>();
            Parallel.For((int)startBlock.Value, (int)currentBlock.Value, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, b =>
            {
                _log.Debug($"Processing Block: {b.ToString()}");

                // Initilize null object to be accessible outside try/catch scope
                Task<Nethereum.RPC.Eth.DTOs.BlockWithTransactions> block = null;

                // Retrieve Transactions inside block X
                try
                {
                    block = _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger((BigInteger)b));
                    block.Wait();
                }
                catch (Exception e)
                {
                    _log.Error(Dump(e));
                }

                if (block == null) return;
                {
                    foreach (var e in block.Result.Transactions)
                    {
                        // Filling the blocking collection
                        trx.Add(e);
                    }
                }
            });
            return trx;
        }

        private static string Dump(object o)
        {
            return JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

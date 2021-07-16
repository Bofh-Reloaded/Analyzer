using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using log4net;
using Microsoft.Extensions.Configuration;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace AnalyzerCore.Services
{
    public class AnalyzerService : BackgroundService
    {
        private const string TokenAddressToCompareWith = "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178";
        private const string SyncEventAddress = "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        // Array of block to analyze
        private static readonly List<int> NumbersOfBlocksToAnalyze = new List<int> {25, 100, 500};

        // Inizialize an empty list of string that will be filled with addresses
        private readonly List<string> _addresses;

        // String Value representing the chain name
        private readonly string _chainName;

        // Initialize Logger
        private readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );

        // Define the delay between one cycle and another
        private const int TaskDelayMs = 360000;

        // Define the TelegramNotifier Instance
        private readonly TelegramNotifier _telegramNotifier;

        // Define Web3 (Nethereum) client
        private readonly Web3 _web3;

        // Initiliaze configuration accessor
        public IConfigurationRoot Configuration;

        private readonly int _blockDurationTime;
        
        private readonly int _maxParallelism;
        
        private readonly string _ourAddress;

        private readonly TokenListConfig _tokenList;

        private readonly List<MissingToken> _missingTokens = new();

        public AnalyzerService(string chainName, string uri, List<string> addresses, TelegramNotifier telegramNotifier,
            int blockDurationTime, int maxParallelism, string ourAddress)
        {
            if (blockDurationTime <= 0) throw new ArgumentOutOfRangeException(nameof(blockDurationTime));
            if (maxParallelism <= 0) throw new ArgumentOutOfRangeException(nameof(maxParallelism));
            // Filling instance variable
            _chainName = chainName ?? throw new ArgumentNullException(nameof(chainName));
            _addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
            _telegramNotifier = telegramNotifier ?? throw new ArgumentNullException(nameof(telegramNotifier));
            _blockDurationTime = blockDurationTime;
            _maxParallelism = maxParallelism;
            _ourAddress = ourAddress ?? throw new ArgumentNullException(nameof(ourAddress));
            _addresses.Add(ourAddress);

            // Load configuration regarding tokens
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("tokens.json", false, true)
                .Build();
            _tokenList = configuration.Get<TokenListConfig>();

            // Registering Nethereum Web3 client endpoint
            _log.Info($"AnalyzerService Initialized for chain: {chainName}");
            _web3 = new Web3(uri);
        }


        private async Task<HexBigInteger> GetCurrentBlock()
        {
            try
            {
                return await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            }
            catch (RpcClientTimeoutException)
            {
                _log.Error($"Cannot connect to RPC for chain: {_chainName}");
                throw;
            }
        }

        private List<Transaction> GetSuccessfulTransactions(
            IEnumerable<Transaction> trxToAnalyze,
            CancellationToken stoppingToken,
            string currentAddress)
        {
            var succededTrxs = new BlockingCollection<Transaction>();
            Parallel.ForEach(trxToAnalyze, new ParallelOptions {MaxDegreeOfParallelism = _maxParallelism}, async t =>
                {
                    _log.Debug($"Getting Receipt for trx hash: {t.TransactionHash}");

                    // Initialize receipt variable
                    Task<TransactionReceipt> receipt = null;

                    // Try to get the transaction receipt
                    try
                    {
                        receipt =
                            _web3.Eth.Transactions.GetTransactionReceipt
                                .SendRequestAsync(t.TransactionHash);
                        receipt.Wait(stoppingToken);
                    }
                    catch (Exception e)
                    {
                        _log.Error(e.ToString());
                    }
                    
                    if (!(receipt is {Result: { }})) return;
                    if (!receipt.Result.Succeeded() || receipt.Result.Logs.Count <= 0) return;
                    _log.Debug($"Succeeded trx with hash: {t.TransactionHash}");
                    await AnalyzeMissingTokens(currentAddress, receipt);
                    succededTrxs.Add(t, stoppingToken);
                });
            return succededTrxs.ToList();
        }

        private async Task AnalyzeMissingTokens(string currentAddress, Task<TransactionReceipt> receipt)
        {
            // Analyze Tokens
            if (!string.Equals(currentAddress, TokenAddressToCompareWith,
                StringComparison.CurrentCultureIgnoreCase)) return;
            {
                var logsList = receipt.Result.Logs.ToList();
                var syncEvents = logsList.Where(
                    e => string.Equals(e["topics"][0].ToString().ToLower(),
                        SyncEventAddress, StringComparison.Ordinal)
                ).ToList();
                foreach (var contractHandler in syncEvents.Select(contract => contract["address"]
                        .ToString())
                    .Select(contractAddress => _web3.Eth.GetContractHandler(contractAddress)))
                {
                    var token0OutputDto =
                        await contractHandler.QueryDeserializingToObjectAsync<Token0Function, Token0OutputDTO>();
                    var poolFactory = await contractHandler.QueryAsync<FactoryFunction, string>();
                    var token1OutputDto =
                        await contractHandler.QueryDeserializingToObjectAsync<Token1Function, Token1OutputDTO>();
                    var token0 = token0OutputDto.ReturnValue1;
                    var token1 = token1OutputDto.ReturnValue1;
                    EvaluateToken(token0, receipt, poolFactory);
                    EvaluateToken(token1, receipt, poolFactory);
                }
            }
        }

        private void EvaluateToken(string token, Task<TransactionReceipt> receipt, string factory)
        {
            if (_tokenList.whitelisted.Contains(token) || _tokenList.blacklisted.Contains(token)) return;
            if ((_missingTokens.Where(m => m.TokenAddress == token)).Any()) return;
            _log.Info(
                $"Found missing token: {token} with txhash: {receipt.Result.TransactionHash} within exchange: {factory}");
            _missingTokens.Add(new MissingToken()
            {
                PoolFactory = factory,
                TokenAddress = token,
                TransactionHash = receipt.Result.TransactionHash
            });
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

                var msg = new Message
                {
                    Addresses = new List<AddressStats>(),
                    Timestamp =
                        $"<b>\U0001F550[{DateTime.Now.ToString(CultureInfo.InvariantCulture)}]\U0001F550</b>"
                };

                // Get Current Block
                var currentBlock = await GetCurrentBlock();

                if (currentBlock is null)
                {
                    _log.Error($"Cannot retrieve currentBlock, stopping service: {_chainName}");
                    break;
                }

                var startBlock = new HexBigInteger(currentBlock.Value - NumbersOfBlocksToAnalyze.Max());

                // Get all the transactions inside the blocks between latest and latest - 500
                var trx = GetBlocksAsync(startBlock, currentBlock);
                _log.Info($"Total trx: {trx.Count.ToString()}");

                /* Checking succeded transactions */
                foreach (var address in _addresses)
                {
                    var addrStats = new AddressStats {Address = address, BlockRanges = new List<BlockRangeStats>()};
                    var addrTrxs = trx.Where(t => t.IsFrom(address) || t.IsTo(address)).ToList();

                    _log.Info($"Evaluating Address: {address} with trx amount: {addrTrxs.Count.ToString()}");

                    foreach (var numberOfBlocks in NumbersOfBlocksToAnalyze.OrderBy(i => i))
                    {
                        _log.Info(
                            // ReSharper disable once HeapView.BoxingAllocation
                            $"NB: {numberOfBlocks.ToString()} Evaluating SB: {currentBlock.Value - numberOfBlocks} TB: {currentBlock.Value.ToString()}");

                        var trxToAnalyze =
                            addrTrxs.Where(t => t.BlockNumber >= currentBlock.Value - numberOfBlocks)
                                .ToList();
                        var succededTrxs =
                            GetSuccessfulTransactions(trxToAnalyze, stoppingToken, address);

                        _log.Info($"TRX to analyze: {trxToAnalyze.Count().ToString()}");

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
                            if (string.Equals(address.ToLower(), _ourAddress.ToLower(), StringComparison.Ordinal))
                            {
                                // Analyze the stats for type of trade
                                blockRangeStats.T0Trx = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.T0}"))
                                    .ToList();
                                blockRangeStats.T1Trx = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.T1}"))
                                    .ToList();
                                blockRangeStats.T2Trx = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.T2}"))
                                    .ToList();
                                blockRangeStats.ContP = trxToAnalyze.Where(t => t.Input.StartsWith($"0x{OpCodes.Cont}"))
                                    .ToList();
                                blockRangeStats.T0TrxSucceded = 
                                    succededTrxs.Where(t => t.Input.StartsWith($"0x{OpCodes.T0}"))
                                        .ToList();
                                blockRangeStats.T1TrxSucceded =
                                    succededTrxs.Where(t => t.Input.StartsWith($"0x{OpCodes.T1}"))
                                        .ToList();
                                blockRangeStats.T2TrxSucceded =
                                    succededTrxs.Where(t => t.Input.StartsWith($"0x{OpCodes.T2}"))
                                        .ToList();
                                blockRangeStats.ContPSucceded = succededTrxs
                                    .Where(t => t.Input.StartsWith($"0x{OpCodes.Cont}"))
                                    .ToList();
                            }

                            addrStats.BlockRanges.Add(blockRangeStats);
                        }
                        catch (DivideByZeroException)
                        {
                            _log.Error("No transaction retrieved");
                        }
                    }

                    msg.Addresses.Add(addrStats);
                }

                msg.TotalTrx = trx.Count;
                msg.TPS = trx.Count / _blockDurationTime;

                msg.ourAddress = _ourAddress;

                _telegramNotifier.SendStatsRecap(msg);
                if (_missingTokens.Count > 0)
                {
                    var missingTokenMessage = _missingTokens.Select(missingToken =>
                            $"[Token: {missingToken.TokenAddress}, Exchange: {missingToken.PoolFactory}]{Environment.NewLine}")
                        .ToList();
                    _telegramNotifier.SendMessage("\U00002728 <b>Missing Tokens</b> \U00002728");
                    _telegramNotifier.SendMessage(string.Join(
                        Environment.NewLine,
                        missingTokenMessage.ToArray()));
                }
                    
                    
                _missingTokens.Clear();

                await Task.Delay(TaskDelayMs, stoppingToken);
            }
        }

        private BlockingCollection<Transaction> GetBlocksAsync(HexBigInteger startBlock, HexBigInteger currentBlock)
        {
            var trx = new BlockingCollection<Transaction>();
            Parallel.For((int) startBlock.Value, (int) currentBlock.Value,
                new ParallelOptions {MaxDegreeOfParallelism = _maxParallelism}, b =>
                {
                    _log.Debug($"Processing Block: {b.ToString()}");

                    // Initilize null object to be accessible outside try/catch scope
                    Task<BlockWithTransactions> block = null;

                    // Retrieve Transactions inside block X
                    try
                    {
                        block = _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                            .SendRequestAsync(new HexBigInteger(b));
                        block.Wait();
                    }
                    catch (Exception e)
                    {
                        _log.Error(Dump(e));
                    }

                    if (block == null) return;
                    {
                        foreach (var e in block.Result.Transactions)
                            // Filling the blocking collection
                            trx.Add(e);
                    }
                });
            return trx;
        }

        private static string Dump(object o)
        {
            return JsonSerializer.Serialize(o, new JsonSerializerOptions {WriteIndented = true});
        }
    }
}
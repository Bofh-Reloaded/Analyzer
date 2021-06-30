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
using Nethereum.RPC.Eth.DTOs;

namespace AnalyzerCore.Services
{
    public class AnalyzerService : BackgroundService
    {
        // Initialize Logger
        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );

        // Define the delay between one cycle and another
        private readonly int taskDelayMs = 360000;

        // Array of block to analyze
        private static List<int> numbersOfBlocksToAnalyze = new List<int> { 25, 100, 500 };

        // Define the TelegramNotifier Instance
        private TelegramNotifier telegramNotifier;

        // Define Web3 (Nethereum) client
        private Web3 web3;

        // String Value representing the chain name
        private string chainName;

        // Initiliaze configuration accessor
        public IConfigurationRoot configuration;

        // Inizialize an empty list of string that will be filled with addresses
        public List<string> addresses = new List<string>();

        public AnalyzerService(
            string chainName,
            string uri,
            List<string> addresses,
            TelegramNotifier telegramNotifier)
        {
            // Filling instance variable
            this.chainName = chainName;
            this.addresses = addresses;
            this.telegramNotifier = telegramNotifier;

            // Registering Nethereum Web3 client endpoint
            log.Info($"AnalyzerService Initialized for chain: {chainName}");
            web3 = new Web3(uri);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            log.Info($"Starting AnalyzerService for chain: {chainName}");
            telegramNotifier.SendMessage($"Starting AnalyzerService for chain: {chainName}");
            stoppingToken.Register(() =>
                log.Info($"AnalyzerService background task is stopping for chain: {chainName}"));

            while (!stoppingToken.IsCancellationRequested)
            {
                log.Info("New Analsys Cycle");
                var msg = new Message();
                msg.Addresses = new List<AddressStats>();
                msg.Timestamp = $"<b>\U0001F550[{DateTime.Now}]\U0001F550</b>";

                // Get Current Block
                HexBigInteger currentBlock = null;
                try {
                    currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                } catch (Exception e)
                {
                    log.Error(e.ToString());
                    continue;
                }
                    
                HexBigInteger startBlock = new HexBigInteger(
                    (BigInteger)currentBlock.Value - (BigInteger)numbersOfBlocksToAnalyze.Max()
                );

                // Get all the transactions inside the blocks between latest and latest - 500
                var trx = new BlockingCollection<Nethereum.RPC.Eth.DTOs.Transaction>();
                Parallel.For((int)startBlock.Value, (int)currentBlock.Value, new ParallelOptions { MaxDegreeOfParallelism = 8 }, b =>
                {
                    log.Debug($"Processing Block: {b}");

                    // Initilize null object to be accessible outside try/catch scope
                    Task<Nethereum.RPC.Eth.DTOs.BlockWithTransactions> block = null;

                    // Retrieve Transactions inside block X
                    try
                    {
                        block = web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger((BigInteger)b));
                        block.Wait();
                    } catch (Exception e)
                    {
                        log.Error(Dump(e));
                    }
                    
                    if (block != null)
                    {
                        foreach (Nethereum.RPC.Eth.DTOs.Transaction e in block.Result.Transactions)
                        {
                            // Filling the blocking collection
                            trx.Add(e);
                        }
                    }
                });
                log.Info($"Total trx: {trx.Count()}");

                /* Checking succeded transactions */
                foreach (var address in addresses)
                {
                    var addrStats = new AddressStats();
                    addrStats.Address = address;
                    addrStats.BlockRanges = new List<BlockRangeStats>();

                    BlockingCollection<Transaction> addrTrxs = new BlockingCollection<Transaction>();
                    Parallel.ForEach(trx, new ParallelOptions { MaxDegreeOfParallelism = 12 }, tr =>
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
                    log.Info($"Evaluating Address: {address} with trx amount: {addrTrxs.Count()}");
                    foreach (var numberOfBlocks in numbersOfBlocksToAnalyze.OrderBy(i => i))
                    {
                        log.Info($"NB: {numberOfBlocks} Evaluating SB: {currentBlock.Value - numberOfBlocks} TB: {currentBlock.Value}");

                        BlockingCollection<Nethereum.RPC.Eth.DTOs.Transaction> succededTrxs = new BlockingCollection<Nethereum.RPC.Eth.DTOs.Transaction>();
                        var trxToAnalyze = addrTrxs.Where(t => t.BlockNumber >= currentBlock.Value - numberOfBlocks);
                        log.Info($"TRX to analyze: {trxToAnalyze.Count()}");
                        Parallel.ForEach(trxToAnalyze, new ParallelOptions { MaxDegreeOfParallelism = 12 }, _t =>
                        {
                            log.Debug($"Getting Receipt for trx hash: {_t.TransactionHash}");

                            // Initialize receipt variable
                            Task<Nethereum.RPC.Eth.DTOs.TransactionReceipt> receipt = null;

                            // Try to get the transaction receipt
                            try
                            {
                                receipt = web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(_t.TransactionHash);
                                receipt.Wait();
                            }
                            catch (Exception e)
                            {
                                log.Error(e.ToString());
                            }

                            if (receipt.Result != null)
                            {
                                if (receipt.Result.Status.Value.IsOne)
                                {
                                    log.Debug($"Succeeded trx with hash: {_t.TransactionHash}");
                                    succededTrxs.Add(_t);
                                }
                            }
                        });
                        try
                        {
                            // Calculate the success rate and construct che BlockRangeStat object
                            long successRate = 100 * succededTrxs.Count() / trxToAnalyze.Count();
                            BlockRangeStats blockRangeStats = new BlockRangeStats();
                            blockRangeStats.BlockRange = numberOfBlocks;
                            blockRangeStats.SuccededTranstactionsPerBlockRange = succededTrxs.Count();
                            blockRangeStats.TotalTransactionsPerBlockRange = trxToAnalyze.Count();
                            blockRangeStats.SuccessRate = $"{successRate}%";
                            addrStats.BlockRanges.Add(blockRangeStats);
                        }
                        catch (System.DivideByZeroException)
                        {
                            log.Error("No transaction retrieved");
                            continue;
                        }
                    }
                    msg.Addresses.Add(addrStats);
                }

                telegramNotifier.SendStatsRecap(message: msg);
                await Task.Delay(taskDelayMs, stoppingToken);
            }
        }

        private static string Dump(object o)
        {
            return JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

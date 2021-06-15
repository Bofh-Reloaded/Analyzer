using System;
using log4net;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using AnalyzerCore.Libs;
using AnalyzerCore.Models.BscScanModels;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.IO;
using AnalyzerCore.Notifier;
using Nethereum.Web3;
using Nethereum.Hex.HexTypes;
using System.Numerics;
using AnalyzerCore.Models;
using System.Collections.Concurrent;

namespace AnalyzerCore.Services
{
    public class PlyAnalyzerService : BackgroundService
    {
        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        private readonly int taskDelayMs = 360000;
        private BscScan bscScanApiClient = new BscScan();
        private static List<int> numbersOfBlocksToAnalyze = new List<int> { 25, 100, 500 };
        private TelegramNotifier telegramNotifier = new TelegramNotifier(chatId: "-532850503");
        private Web3 web3 = new Web3("https://rpc-mainnet.maticvigil.com");
        private string ourAddress;

        public IConfigurationRoot configuration;
        public List<string> addresses = new List<string>();
        public Dictionary<string, List<Result>> SharedData = new Dictionary<string, List<Result>>(); 
      
        public PlyAnalyzerService(Dictionary<string, List<Result>> data)
        {
            log.Info("AnalyzerService Starting");
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appSettings.json", false, reloadOnChange: true)
                .Build();
            Options options = new Options();


            configuration.GetSection(nameof(Options)).Bind(options);

            var _s = configuration.GetSection("PlyAddress");
            var ourAddress = _s.Get<string>();
            var section = configuration.GetSection("PlyEnemies");
            try {
                var addresses = section.Get<List<string>>();
            } catch (System.NullReferenceException)
            {
                var addresses = new List<string>();
            }
            addresses.Add(ourAddress);
            this.SharedData = data;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            log.Info("Starting PlyAnalyzerService");
            telegramNotifier.SendMessage("Starting PlyAnalyzerService");
            stoppingToken.Register(() =>
                log.Info("plyAnalyzerService background task is stopping"));

            while (!stoppingToken.IsCancellationRequested)
            {
                log.Info("New Analsys Cycle");
                var msg = new Message();
                msg.Addresses = new List<AddressStats>();
                msg.Timestamp = $"*[{DateTime.Now}]*";

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
                    log.Info($"Processing Block: {b}");

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

                    var addrTrxs = trx.Where(tr => tr.From.ToLower() == address.ToLower());
                    log.Info($"Evaluating Address: {address} with trx amount: {addrTrxs.Count()}");
                    foreach (var numberOfBlocks in numbersOfBlocksToAnalyze.OrderBy(i => i))
                    {
                        log.Info($"Evaluating from block: {currentBlock.Value - numberOfBlocks} to block: {currentBlock.Value}");

                        BlockingCollection<Nethereum.RPC.Eth.DTOs.Transaction> succededTrxs = new BlockingCollection<Nethereum.RPC.Eth.DTOs.Transaction>();
                        foreach (var _t in addrTrxs.Where(t => t.BlockNumber >= currentBlock.Value - numberOfBlocks))
                        {
                            log.Info($"Getting Receipt for trx hash: {_t.TransactionHash}");

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
                            
                            
                            log.Info(Dump(receipt.Result.TransactionIndex));
                            if (receipt.Result != null)
                            {
                                if (receipt.Result.Status.Value.IsOne)
                                {
                                    log.Info($"Succeeded trx with hash: {_t.TransactionHash}");
                                    succededTrxs.Add(_t);
                                }
                            }
                        }

                        try
                        {
                            long successRate = 100 * succededTrxs.Count() / addrTrxs.Take(numberOfBlocks).Count();
                            BlockRangeStats blockRangeStats = new BlockRangeStats();
                            blockRangeStats.BlockRange = numberOfBlocks;
                            blockRangeStats.SuccededTranstactionsPerBlockRange = succededTrxs.Count();
                            blockRangeStats.TotalTransactionsPerBlockRange = addrTrxs.Take(numberOfBlocks).Count();
                            blockRangeStats.SuccessRate = $"{successRate}%";
                            addrStats.BlockRanges.Add(blockRangeStats);
                        }
                        catch (System.DivideByZeroException)
                        {
                            log.Error("Dio Cane");
                            continue;
                        }
                    }
                    msg.Addresses.Add(addrStats);
                }

                log.Info(Dump(msg));
                telegramNotifier.SendMessage(JsonSerializer.Serialize(msg, new JsonSerializerOptions { WriteIndented = true }).ToString());
                
                await Task.Delay(taskDelayMs, stoppingToken);
            }
        }


        private static string Dump(object o)
        {
            return JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

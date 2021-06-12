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

namespace AnalyzerCore.Services
{
    public class Options
    {
        public List<string> addresses { get; set; }
        public string ourAddress { get; set; }
    }

    public class BlockRangeStats
    {
        public int BlockRange { get; set; }
        public int SuccededTranstactionsPerBlockRange { get; set; }
        public int TotalTransactionsPerBlockRange { get; set; }
        public string SuccessRate { get; set; }
    }
    public class AddressStats
    {
        public string Address { get; set; }
        public List<BlockRangeStats> BlockRanges { get; set; }
    }

    public class Message
    {
        public string Timestamp { get; set; }
        public List<AddressStats> Addresses { get; set; }
    }

    public class AnalyzerService : BackgroundService
    {
        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        private readonly int taskDelayMs = 120000;
        private BscScan bscScanApiClient = new BscScan();
        private static List<int> numbersOfBlocksToAnalyze = new List<int> { 25, 100, 500 };
        public IConfigurationRoot configuration;
        public List<string> addresses = new List<string>();
        private TelegramNotifier telegramNotifier = new TelegramNotifier();
        public Dictionary<string, List<Result>> SharedData = new Dictionary<string, List<Result>>(); 
        private Web3 web3 = new Web3("https://bsc-dataseed.binance.org");

        private string ourAddress;

        public AnalyzerService(Dictionary<string, List<Result>> data)
        {
            log.Info("AnalyzerService Starting");
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appSettings.json", false, reloadOnChange: true)
                .Build();
            Options options = new Options();


            configuration.GetSection(nameof(Options)).Bind(options);

            //ourAddress = configuration.GetSection("ourAddress").Get<string>();
            ourAddress = "0x153e170524cfad4261743ce8bd8053e15d6d1f15";
            var section = configuration.GetSection("enemies");
            addresses = section.Get<List<string>>();
            addresses.Add(ourAddress);

            this.SharedData = data;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            log.Info("Starting AnalyzerService");
            telegramNotifier.SendMessage("Starting AnalyzerService");
            stoppingToken.Register(() =>
                log.Info("AnalyzerService background task is stopping"));

            while (!stoppingToken.IsCancellationRequested)
            {
                log.Info("New Analsys Cycle");
                var msg = new Message();
                msg.Addresses = new List<AddressStats>();
                msg.Timestamp = $"*[{DateTime.Now}]*";

                HexBigInteger currentBlock = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();

                //Retrieve Transactions for the last n blocks
                int startBlock = (int)(currentBlock.Value - numbersOfBlocksToAnalyze.Max());

                foreach (var address in addresses)
                {
                    var addrStats = new AddressStats();
                    var trx = new List<Result>();
                    try
                    {
                        trx = await GetTransactionsByAddressAsync(address, startBlock.ToString(), endBlock: currentBlock.Value.ToString());
                    } catch
                    {
                        log.Error($"Cannot retrieve transactions for address: {address}, skipping.");
                        continue;
                    }
                    SharedData[address] = trx;
                    log.Info($"[{address}] => Total: {trx.Count} trx retrieved.");
                    addrStats.Address = $"*[{address}]*";
                    addrStats.BlockRanges = new List<BlockRangeStats>();

                    // Start Analyze
                    foreach (var numberOfBlocks in numbersOfBlocksToAnalyze.OrderBy(i => i))
                    {
                        int firstBlock = (int)currentBlock.Value - numberOfBlocks;
                        var transactions = trx.Where(tr => int.Parse(tr.blockNumber) >= firstBlock);
                        var successTransactions = transactions.Where(tr => tr.txreceipt_status == "1");
                        try
                        {
                            long successRate = 100 * successTransactions.Count() / transactions.Count();
                            BlockRangeStats blockRangeStats = new BlockRangeStats();
                            blockRangeStats.BlockRange = numberOfBlocks;
                            blockRangeStats.SuccededTranstactionsPerBlockRange = successTransactions.Count();
                            blockRangeStats.TotalTransactionsPerBlockRange = transactions.Count();
                            blockRangeStats.SuccessRate = $"{successRate}%";
                            addrStats.BlockRanges.Add(blockRangeStats);
                        }
                        catch (System.DivideByZeroException)
                        {
                            continue;
                        }
                        Thread.Sleep(50);
                    }
                    msg.Addresses.Add(addrStats);
                }

                telegramNotifier.SendMessage(JsonSerializer.Serialize(msg, new JsonSerializerOptions { WriteIndented = true }).ToString());

                await Task.Delay(taskDelayMs, stoppingToken);
            }



        }

        public async Task<List<Result>> GetTransactionsByAddressAsync(string address, string startBlock, string endBlock)
        {
            var trx = await bscScanApiClient.RetrieveTransactionsAsync(
                        address: address,
                        startBlock: startBlock,
                        endBlock: endBlock);
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
            return trx.result;
        }
    }
}

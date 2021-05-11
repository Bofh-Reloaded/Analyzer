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
using Range = AnalyzerCore.Models.Range;

namespace AnalyzerCore.Services
{
    public class Options
    {
        public List<string> addresses { get; set; }
        public string ourAddress { get; set; }
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
                List<string> tgMsgs = new List<string>();
                tgMsgs.Add($"*[{DateTime.Now}]*");
                //Getting Current Block
                CurrentBlock currentBlock = await bscScanApiClient.GetCurrentBlock();
                if (currentBlock.message == "NOTOK")
                {
                    log.Warn("bscscan is lagging, skipping cycle.");
                    telegramNotifier.SendMessage("bscscan is lagging, skipping cycle.");
                    await Task.Delay(taskDelayMs, stoppingToken);
                    continue;
                }

                //Retrieve Transactions for the last n blocks
                int startBlock = int.Parse(currentBlock.result) - numbersOfBlocksToAnalyze.Max();

                foreach (var address in addresses)
                {
                    var trx = await GetTransactionsByAddressAsync(address, startBlock.ToString(), endBlock: currentBlock.result);
                    SharedData[address] = trx;
                    log.Info($"[{address}] => Total: {trx.Count} trx retrieved.");
                    tgMsgs.Add($"*[{address}]*");

                    // Start Analyze
                    foreach (var numberOfBlocks in numbersOfBlocksToAnalyze.OrderBy(i => i))
                    {
                        int firstBlock = int.Parse(currentBlock.result) - numberOfBlocks;
                        var transactions = trx.Where(tr => int.Parse(tr.blockNumber) >= firstBlock);
                        if (address == ourAddress)
                        {
                            tgMsgs.Add($" *Block Range: {firstBlock.ToString()} to {currentBlock.result}: {numberOfBlocks} blocks.*");
                        }
                        var successTransactions = transactions.Where(tr => tr.txreceipt_status == "1");
                        try
                        {
                            long successRate = 100 * successTransactions.Count() / transactions.Count();
                            string _msg = "";
                            if (address == ourAddress)
                            {
                                _msg = $" -> Total trx: {transactions.Count()}; Successfull: {successTransactions.Count()}; SR: *{successRate}%*";
                            }
                            else
                            {
                                _msg = $"B: {numberOfBlocks} S TRX: {successTransactions.Count()}/{transactions.Count()}; SR: *{successRate}%*";
                            }

                            tgMsgs.Add(_msg);
                        }
                        catch (System.DivideByZeroException)
                        {
                            tgMsgs.Add("No Transaction in this interval");
                        }
                        if (address == ourAddress)
                        {
                            tgMsgs.Add($" --> Result of Gas Analysis on this cycle");
                            List<Range> ranges = new List<Range>();
                            ranges.Add(new Range
                            {
                                rangeName = "x<=5000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) <= 5000005000).ToList()
                            });
                            ranges.Add(new Range
                            {
                                rangeName = "5000005000>x<7000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) > 5000005000 && long.Parse(x.gasPrice) < 7000005000).ToList()
                            });
                            ranges.Add(new Range
                            {
                                rangeName = "7000005000>x<10000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) > 7000005000 && long.Parse(x.gasPrice) < 10000005000).ToList()
                            });
                            ranges.Add(new Range
                            {
                                rangeName = "10000005000>x<15000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) > 10000005000 && long.Parse(x.gasPrice) < 15000005000).ToList()
                            });
                            ranges.Add(new Range
                            {
                                rangeName = "15000005000>x<25000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) > 15000005000 && long.Parse(x.gasPrice) < 25000005000).ToList()
                            });
                            ranges.Add(new Range
                            {
                                rangeName = "25000005000>x<35000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) > 25000005000 && long.Parse(x.gasPrice) < 35000005000).ToList()
                            });
                            ranges.Add(new Range
                            {
                                rangeName = "35000005000>x<45000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) > 35000005000 && long.Parse(x.gasPrice) < 45000005000).ToList()
                            });
                            ranges.Add(new Range
                            {
                                rangeName = "45000005000>x<60000005000",
                                trxInRange = transactions.Where(x => long.Parse(x.gasPrice) > 45000005000 && long.Parse(x.gasPrice) < 60000005000).ToList()
                            });
                            foreach (var range in ranges)
                            {
                                try
                                {
                                    long sr = 100 * range.trxInRange.Where(x => x.txreceipt_status == "1").Count() / range.trxInRange.Count();
                                    tgMsgs.Add($" --> Range: {range.rangeName}, avgGas: {range.trxInRange.Select(x => long.Parse(x.gasPrice)).ToList().Sum() / range.trxInRange.Count()}, total trx: {range.trxInRange.Count()}, success rate: {sr}%");
                                }
                                catch (System.DivideByZeroException)
                                {
                                    log.Debug($"No trx in gas Range: {range.rangeName}");
                                }
                            }
                        }
                    }

                }

                string finalMsg = string.Join(Environment.NewLine, tgMsgs.ToArray());
                telegramNotifier.SendMessage(finalMsg);

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

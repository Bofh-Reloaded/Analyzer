using System;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using log4net;
using log4net.Config;
using log4net.Core;
using System.Text.Json;
using AnalyzerCore.Libs;
using System.Collections.Generic;
using System.Linq;
using AnalyzerCore.Notifier;
using System.Threading;
using AnalyzerCore.Models.BscScanModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections;

namespace AnalyzerCore
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        static readonly string OurAddress = "0x153e170524cfad4261743ce8bd8053e15d6d1f15";
        private static BscScan bscScanApiClient = new BscScan();
        private static List<int> numbersOfBlocksToAnalyze = new List<int> { 25, 100, 500 };
        //implementare comando via bot per richiamare analisi all'interno di un block range
        private static TelegramNotifier telegramNotifier = new TelegramNotifier();
        public static IConfigurationRoot configuration;


        /*
        static TransactionReceipt()
        {
            
                // Create HTTP Client (to be moved inside a container class)
                HttpClient c = new HttpClient();
                c.DefaultRequestHeaders.Accept.Clear();
                c.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json")
                    );

                Parallel.ForEach(trx.result, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async tr =>
                {
                    // Retrieve Event Log for the trx
                    log.Debug($"Current trx: {tr.hash} and block: {tr.blockNumber} txreceipt_status: {tr.txreceipt_status}");
                    //log.Debug(JsonConvert.SerializeObject(tr, Formatting.Indented));
                    TransactionReceiptJsonRequest jsonRequest = new TransactionReceiptJsonRequest();
                    jsonRequest.Id = 1;
                    jsonRequest.Method = "eth_getTransactionReceipt";
                    jsonRequest.Jsonrpc = "2.0";
                    jsonRequest.Params = new List<string>();
                    jsonRequest.Params.Add(tr.hash);

                    var content = new StringContent(JsonConvert.SerializeObject(jsonRequest), Encoding.UTF8, "application/json");
                    try
                    {
                        HttpResponseMessage response = await c.PostAsync(requestUri: "http://18.192.76.89:8545/ ", content: content);
                        TransactionReceipt trr = JsonConvert.DeserializeObject<TransactionReceipt>(await response.Content.ReadAsStringAsync());
                        log.Debug(JsonConvert.SerializeObject(trr.result, Formatting.Indented));
                    } catch (Exception ex)
                    {
                        log.Error($"Failed to retrieve Transaction Receipt for trx: {tr.hash} with Exception: {ex}");
                    }   
                });
                
        }
        */

        public static async Task<List<Result>> GetTransactionsByAddressAsync(string address, string startBlock, string endBlock)
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

        private static List<Result> GetFailedTrxWithHiGas(List<Result> transactions, long gasTrigger = 40000) {
            var toNotify = transactions
                .Where(tr => int.Parse(tr.txreceipt_status) == 0)
                .Where(tr => long.Parse(tr.gasUsed) >= gasTrigger).ToList();
            return toNotify;
        }

        private static async Task MainAsync()
        {
            log.Info("Creating Service Collection");

            var Addresses = new List<string>()
            {
                OurAddress,
                "0x135950adfda533dc212535093c4c4e5a62fc9195",
                "0x6dd596eec44067d80ca2122e757ab806f551e521",
                "0x23267395057554d62e144323d0fa7dc0c0550d69",
                "0x1ad83ec9cc98aca1898fd1c9e4475717851301f9"
            };
            List<string> trxHashAlerted = new List<string>();

            log.Info("Bot Started");

            while (true)
            {
                List<string> tgMsgs = new List<string>();
                tgMsgs.Add($"*[{DateTime.Now}]*");
                //Getting Current Block
                string currentBlock = await bscScanApiClient.GetCurrentBlock();

                //Retrieve Transactions for the last n blocks
                int startBlock = int.Parse(currentBlock) - numbersOfBlocksToAnalyze.Max();

                foreach (var address in Addresses) {
                    var trx = await GetTransactionsByAddressAsync(address, startBlock.ToString(), endBlock: currentBlock);
                    log.Info($"Total: {trx.Count} trx retrieved.");
                    tgMsgs.Add($"*[{address}]*");

                    // Start Analyze
                    foreach (var numberOfBlocks in numbersOfBlocksToAnalyze.OrderBy(i => i))
                    {
                        int firstBlock = int.Parse(currentBlock) - numberOfBlocks;
                        var transactions = trx.Where(tr => int.Parse(tr.blockNumber) >= firstBlock);
                        tgMsgs.Add($" *Block Range: {firstBlock.ToString()} to {currentBlock}: {numberOfBlocks} blocks.*");
                        var successTransactions = transactions.Where(tr => tr.txreceipt_status == "1");
                        try
                        {
                            long successRate = 100 * successTransactions.Count() / transactions.Count();
                            string _msg = $" -> Total trx: {transactions.Count()}; Successfull: {successTransactions.Count()}; SR: *{successRate}%*";
                            tgMsgs.Add(_msg);
                        }
                        catch (System.DivideByZeroException)
                        {
                            tgMsgs.Add("No Transaction in this interval");
                        }
                        if (address == OurAddress)
                        {
                            tgMsgs.Add($" --> Result of Gas Analysis on this cycle");
                            /*
                             * gasPrice <= 5.000005
                             * gasPrice > 5.000005 && gasPrice < 7.500005
                             * gasPrice >= 7.500005 && gasPrice < 10
                             * gasPrice >= 10 && gasPrice < 15
                             * gasPrice >= 15 && gasPrice < 25
                             * gasPrice >= 25 && gasPrice < 35
                             * gasPrice >= 35 && gasPrice < 45
                             * gasPrice >= 45 && gasPrice < 60
                             * else*/
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
                                    tgMsgs.Add(" --> No trx in this interval");
                                }
                            }
                        }
                    }

                    // Analyze Failed trx
                    if (address == OurAddress) {
                        var t = GetFailedTrxWithHiGas(transactions: trx);
                        var trxToNotify = t.Where(t => !trxHashAlerted.Contains(t.hash)).ToList();
                        foreach(var tn in trxToNotify)
                        {
                            telegramNotifier.SendMessage($"Tx failed: https://bscscan.com/tx/{tn.hash} with gasUsed: {tn.gasUsed}");
                            trxHashAlerted.Add(tn.hash);
                        }

                    }
                    
                }

                string finalMsg = string.Join(Environment.NewLine, tgMsgs.ToArray());
                telegramNotifier.SendMessage(finalMsg);
                Thread.Sleep(120000);
            }
        }

        static void Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appSettings.json", false, reloadOnChange: true)
                .Build();

            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;

            //CreateHostBuilder(args).Build().Run();

            try
            {
                MainAsync().Wait();
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        /*public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddHostedService<VideosWatcher>();
            });*/

        public class Range : IEnumerable
        {
            public string rangeName { get; set; }
            public List<Result> trxInRange { get; set; }

            public IEnumerator GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }
    }
}

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
using Microsoft.Extensions.Configuration.Json;



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

        static void main(string[] args)
        {
            try
            {
                MainAsync().Wait();
            } catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        static async Task MainAsync()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appSettings.json", false, reloadOnChange: true)
                .Build();
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;
            var Addresses = new List<string>();
            configuration.GetSection("enemies").Bind(Addresses);
            Addresses.Insert(0, OurAddress);
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
                        tgMsgs.Add($" Block Range: {firstBlock.ToString()} to {currentBlock}: {numberOfBlocks} blocks.");
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
                    }
                }

                string finalMsg = string.Join(Environment.NewLine, tgMsgs.ToArray());
                telegramNotifier.SendMessage(finalMsg);
                Thread.Sleep(120000);
            }
        }
    }
}

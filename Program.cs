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

namespace AnalyzerCore
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        static readonly string enemyAddress = "0x135950adfda533dc212535093c4c4e5a62fc9195";
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

        static async Task Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;
            List<string> Addresses = new List<string> {
                OurAddress,
                enemyAddress,
                "0x6dd596eec44067d80ca2122e757ab806f551e521",
                "0x380cD66bCA70e8cd5eb8F4e17b59594837F1C339",
                "0xfb438dc206e3e29f83748ad1a785b83f43cde553"
            };

            log.Info("Starting Bot.");

            while (true)
            {
                List<string> tgMsgs = new List<string>();
                //Getting Current Block
                string currentBlock = await bscScanApiClient.GetCurrentBlock();

                //Retrieve Transactions for the last n blocks
                int startBlock = int.Parse(currentBlock) - numbersOfBlocksToAnalyze.Max();

                foreach (var address in Addresses)
                {
                    log.Info($"Retrieving Transaction for Address: {address} from block: {startBlock.ToString()} to block: {currentBlock}");
                    var trx = await bscScanApiClient.RetrieveTransactionsAsync(
                        address: address,
                        startBlock: startBlock.ToString(),
                        endBlock: currentBlock);
                    var options = new JsonSerializerOptions()
                    {
                        WriteIndented = true
                    };
                    log.Info($"Total: {trx.result.Count} trx retrieved.");
                    tgMsgs.Add($"*[{address}]*");

                    // Start Analyze
                    foreach (var numberOfBlocks in numbersOfBlocksToAnalyze.OrderBy(i => i))
                    {
                        int firstBlock = int.Parse(currentBlock) - numberOfBlocks;
                        var transactions = trx.result.Where(tr => int.Parse(tr.blockNumber) >= firstBlock);
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

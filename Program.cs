using System;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using log4net;
using log4net.Config;
using System.Text.Json;
using AnalyzerCore.Libs;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using AnalyzerCore.Models.GethModels;
using System.Collections.Generic;
using System.Text;

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

        static async Task Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            log.Info("Starting Bot.");

            //Getting Current Block
            string currentBlock = await bscScanApiClient.GetCurrentBlock();

            //Retrieve Transactions for the last 20 blocks
            int startBlock = int.Parse(currentBlock) - 20;

            log.Info(
                $"Retrieving Transaction for Address: {OurAddress} from block: {startBlock.ToString()} to block: {currentBlock}"
                );
            var trx = await bscScanApiClient.RetrieveTransactionsAsync(
                address: OurAddress,
                startBlock: startBlock.ToString(),
                endBlock: currentBlock);
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
            log.Info($"Total: {trx.result.Count} trx retrieved.");

            // Create HTTP Client (to be moved inside a container class)
            HttpClient c = new HttpClient();
            c.BaseAddress =new Uri(
                "https://apis.ankr.com/"
                );
            c.DefaultRequestHeaders.Accept.Clear();
            c.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
                );

            Parallel.ForEach(trx.result, async tr =>
            {
                // Retrieve Event Log for the trx
                log.Debug($"Current trx: {tr.hash} and block: {tr.blockNumber} isError: {tr.isError}");
                log.Debug(JsonConvert.SerializeObject(tr, Formatting.Indented));
                TransactionReceiptJsonRequest jsonRequest = new TransactionReceiptJsonRequest();
                jsonRequest.Id = 1;
                jsonRequest.Method = "eth_getTransactionReceipt";
                jsonRequest.Jsonrpc = "2.0";
                jsonRequest.Params = new List<string>();
                jsonRequest.Params.Add(tr.hash);
                var content = new StringContent(JsonConvert.SerializeObject(jsonRequest), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await c.PostAsync(
                    requestUri: "0d8c6d3d16b64f1bb785a96222b54a2b/37a3115e55a6647702c9bd3291129f5d/binance/full/main",
                    content: content
                    );
                TransactionReceipt trr = JsonConvert.DeserializeObject<TransactionReceipt>(await response.Content.ReadAsStringAsync());
                //log.Debug(JsonConvert.SerializeObject(trr, Formatting.Indented));
            });
        }
    }
}

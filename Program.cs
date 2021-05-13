using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using log4net.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AnalyzerCore.Services;
using System.Collections.Generic;
using AnalyzerCore.Models.BscScanModels;

// TODO: implementare comando via bot per richiamare analisi all'interno di un block range

namespace AnalyzerCore
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        public static Dictionary<string, List<Result>> SharedTrxData = new Dictionary<string, List<Result>>();

        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddHostedService<AnalyzerService>(s => new AnalyzerService(SharedTrxData));
                services.AddHostedService<FailedTrxService>(s => new FailedTrxService(SharedTrxData));
                services.AddHostedService<GasAnalyzerService>(s => new GasAnalyzerService(SharedTrxData));
            });
    }
}



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
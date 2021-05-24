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
using Nethereum.Web3;

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
                //services.AddHostedService<FailedTrxService>(s => new FailedTrxService(SharedTrxData));
                services.AddHostedService<GasAnalyzerService>(s => new GasAnalyzerService(SharedTrxData));
            });
    }
}

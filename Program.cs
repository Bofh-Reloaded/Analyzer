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
using Microsoft.Extensions.Configuration;
using System;
using AnalyzerCore.Models;

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
                IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appSettings.json", false, reloadOnChange: true)
                .Build();
                Options options = new Options();
                configuration.GetSection(nameof(Options)).Bind(options);
                var _s = configuration.GetSection("PlyAddress");
                var ourAddress = _s.Get<string>();
                var section = configuration.GetSection("PlyEnemies");
                List<string> addresses = section.Get<List<string>>() ?? new List<string>();
                addresses.Add(ourAddress);

                // Create and add the HostedService for Polygon
                services.AddHostedService<AnalyzerService>(
                    s => new AnalyzerService(
                        chainName: "Polygon",
                        uri: "http://162.55.94.149:8545",
                        addresses: addresses,
                        telegramNotifier: new Notifier.TelegramNotifier(
                            chatId: "-532850503")
                        )
                );
            });
    }
}

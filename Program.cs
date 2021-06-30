using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using log4net.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AnalyzerCore.Services;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System;

namespace AnalyzerCore
{
    class Program
    {
        private static readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );

        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = Level.Info;

            CreateHostBuilder(args).Build().Run();
        }

        public class AnalyzerConfig
        {
            public string PlyAddress { get; set; }
            public string BscAddress { get; set; }
            public List<string> PlyEnemies { get; set; }
            public List<string> BscEnemies { get; set; }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
            // Define Configuration File Reader
            IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
            .AddJsonFile("appSettings.json", false, reloadOnChange: true)
            .Build();

            // Map json configuration inside Object
            var section = configuration.GetSection(nameof(AnalyzerConfig));
            var analyzerConfig = section.Get<AnalyzerConfig>();

            // Adding our own address as last one
            analyzerConfig.PlyEnemies.Add(analyzerConfig.PlyAddress);
            analyzerConfig.BscEnemies.Add(analyzerConfig.BscAddress);

                // Create Polygon Service
                AnalyzerService polygonAnalyzerService = new AnalyzerService(
                    chainName: "Polygon",
                    uri: "http://162.55.94.149:8545",
                    addresses: analyzerConfig.PlyEnemies,
                    telegramNotifier: new Notifier.TelegramNotifier(
                        chatId: "-532850503")
                    );

                // Create and add the HostedService for Polygon
                services.AddHostedService<AnalyzerService>(
                    s => new AnalyzerService(
                        chainName: "Polygon",
                        uri: "http://162.55.94.149:8545",
                        addresses: analyzerConfig.PlyEnemies,
                        telegramNotifier: new Notifier.TelegramNotifier(
                            chatId: "-532850503")
                        )
                    );

                // Create and add the HostedService for Binance Smart Chain
                services.AddHostedService<AnalyzerService>(
                    s => new AnalyzerService(
                        chainName: "Binance Smart Chain",
                        uri: "http://135.148.123.21:8545",
                        addresses: analyzerConfig.BscEnemies,
                        telegramNotifier: new Notifier.TelegramNotifier(
                            chatId: "-560874043")
                        )
                    );
            });
    }
}

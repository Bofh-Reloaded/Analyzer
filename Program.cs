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
            public string HecoAddress { get; set; }
            public List<string> PlyEnemies { get; set; }
            public List<string> BscEnemies { get; set; }
            public List<string> HecoEnemies { get; set; }
        }

        public class ServicesConfig
        {
            public bool BscEnabled { get; set; }
            public bool PlyEnabled { get; set; }
            public bool HecoEnabled { get; set; }
            public bool PlyPendingEnabled { get; set; }
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
                var servicesSection = configuration.GetSection("ServicesConfig");
                var servicesConfig = servicesSection.Get<ServicesConfig>();

                if (servicesConfig.PlyPendingEnabled)
                {
                    services.AddSingleton<IHostedService>(
                        _ => new SubscribedService()
                        );
                }

                if (servicesConfig.PlyEnabled)
                {
                    analyzerConfig.PlyEnemies.Add(analyzerConfig.PlyAddress);
                    // Create and add the HostedService for Polygon
                    services.AddSingleton<IHostedService>(
                        _ => new AnalyzerService(
                            chainName: "Polygon",
                            uri: "http://162.55.94.149:8545",
                            addresses: analyzerConfig.PlyEnemies,
                            telegramNotifier: new Notifier.TelegramNotifier(
                                chatId: "-532850503"),
                            blockDurationTime: 3
                            )
                        );
                }
                
                if (servicesConfig.BscEnabled)
                {
                    analyzerConfig.BscEnemies.Add(analyzerConfig.BscAddress);
                    // Create and add the HostedService for Binance Smart Chain
                    services.AddSingleton<IHostedService>(
                        _ => new AnalyzerService(
                            chainName: "Binance Smart Chain",
                            uri: "http://135.148.123.21:8545",
                            addresses: analyzerConfig.BscEnemies,
                            telegramNotifier: new Notifier.TelegramNotifier(
                                chatId: "-560874043"),
                            blockDurationTime: 5
                            )
                        );
                }

                if (servicesConfig.HecoEnabled)
                {
                    try
                    {
                        analyzerConfig.HecoEnemies.Add(analyzerConfig.HecoAddress);
                    } catch (NullReferenceException)
                    {
                        analyzerConfig.HecoEnemies = new List<string>() { analyzerConfig.HecoAddress };
                    }
                    
                    // Create and add the HostedService for Heco Chain
                    services.AddSingleton<IHostedService>(
                        _ => new AnalyzerService(
                            chainName: "Heco Chain",
                            uri: "http://140.82.61.75:8545",
                            addresses: analyzerConfig.HecoEnemies,
                            telegramNotifier: new Notifier.TelegramNotifier(
                                chatId: "-516536036"),
                            blockDurationTime: 3
                            )
                        );
                }
            });
    }
}

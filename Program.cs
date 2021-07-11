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
using AnalyzerCore.Models;

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
                    // Create and add the HostedService for Polygon
                    services.AddSingleton<IHostedService>(
                        _ => new AnalyzerService(
                            chainName: "Polygon",
                            uri: "http://162.55.94.149:8545",
                            addresses: analyzerConfig.PlyEnemies,
                            telegramNotifier: new Notifier.TelegramNotifier(
                                chatId: "-532850503"),
                            blockDurationTime: 3,
                            maxParallelism: servicesConfig.MaxParallelism,
                            ourAddress: analyzerConfig.PlyAddress
                            )
                        );
                }
                
                if (servicesConfig.BscEnabled)
                {
                    // Create and add the HostedService for Binance Smart Chain
                    services.AddSingleton<IHostedService>(
                        _ => new AnalyzerService(
                            chainName: "BinanceSmartChain",
                            uri: "http://144.76.94.124:8545",
                            addresses: analyzerConfig.BscEnemies,
                            telegramNotifier: new Notifier.TelegramNotifier(
                                chatId: "-560874043"),
                            blockDurationTime: 5,
                            maxParallelism: servicesConfig.MaxParallelism,
                            ourAddress: analyzerConfig.BscAddress
                            )
                        );
                }

                if (servicesConfig.HecoEnabled)
                {
                    // Create and add the HostedService for Heco Chain
                    services.AddSingleton<IHostedService>(
                        _ => new AnalyzerService(
                            chainName: "HecoChain",
                            uri: "http://162.55.99.62:8545",
                            addresses: analyzerConfig.HecoEnemies,
                            telegramNotifier: new Notifier.TelegramNotifier(
                                chatId: "-516536036"),
                            blockDurationTime: 3,
                            maxParallelism: servicesConfig.MaxParallelism,
                            ourAddress: analyzerConfig.HecoAddress
                            )
                        );
                }
            });
    }
}

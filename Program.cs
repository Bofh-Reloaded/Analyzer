using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using AnalyzerCore.Services;
using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AnalyzerCore
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));
            ((Hierarchy) LogManager.GetRepository()).Root.Level = Level.Info;

            CreateHostBuilder(args).Build().Run();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    // Define Configuration File Reader
                    IConfiguration configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                        .AddJsonFile("appSettings.json", false, true)
                        .Build();

                    // Map json configuration inside Object
                    var section = configuration.GetSection(nameof(AnalyzerConfig));
                    var analyzerConfig = section.Get<AnalyzerConfig>();
                    var servicesSection = configuration.GetSection("ServicesConfig");
                    var servicesConfig = servicesSection.Get<ServicesConfig>();
/*
                    if (servicesConfig.PlyEnabled)
                        // Create and add the HostedService for Polygon
                        services.AddSingleton<IHostedService>(
                            _ => new AnalyzerService(
                                "Polygon",
                                "http://162.55.94.149:8545",
                                analyzerConfig.PlyEnemies,
                                new TelegramNotifier(
                                    "-532850503"),
                                3,
                                servicesConfig.MaxParallelism,
                                analyzerConfig.PlyAddress,
                                false
                            )
                        );
*/
                    if (servicesConfig.BscEnabled)
                    {
                        // Temporary fix to get only our addresses
                        var allAddresses = analyzerConfig.BscEnemies;
                        allAddresses.Add(analyzerConfig.BscAddress);
                        var bscDataHandler =
                            new DataCollectorService.ChainDataHandler();
                        services.AddSingleton<IHostedService>(
                            _ => new AnalyzerService(
                                "BinanceSmartChain",
                                analyzerConfig.BscEnemies,
                                new TelegramNotifier(
                                    "-560874043"),
                                5,
                                analyzerConfig.BscAddress,
                                bscDataHandler
                            )
                        );
                        services.AddSingleton<IHostedService>(
                            _ => new TokenObserverService(
                                chainName: "BinanceSmartChain",
                                telegramNotifier: new TelegramNotifier(
                                    "-560874043"),
                                chainDataHandler: bscDataHandler,
                                addressToCompare: "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178")
                        );
                        services.AddSingleton<IHostedService>(
                            _ => new DataCollectorService(
                                "BinanceSmartChain",
                                "http://122.155.166.61:8545",
                                servicesConfig.MaxParallelism,
                                bscDataHandler,
                                allAddresses
                            ));
                    }

                    if (servicesConfig.HecoEnabled)
                    {
                        // Temporary fix to get only our addresses
                        var allAddresses = analyzerConfig.HecoEnemies;
                        allAddresses.Add(analyzerConfig.HecoAddress);
                        var hecoDataHandler =
                            new DataCollectorService.ChainDataHandler();
                        services.AddSingleton<IHostedService>(
                            _ => new AnalyzerService(
                                "HecoChain",
                                analyzerConfig.HecoEnemies,
                                new TelegramNotifier(
                                    "-516536036"),
                                5,
                                analyzerConfig.HecoAddress,
                                hecoDataHandler
                            )
                        );
                        services.AddSingleton<IHostedService>(
                            _ => new TokenObserverService(
                                chainName: "HecoChain",
                                telegramNotifier: new TelegramNotifier(
                                    "-516536036"),
                                chainDataHandler: hecoDataHandler,
                                "0xa5f2b51aa0fa4be37f372622e28ed5a661802a68")
                        );
                        services.AddSingleton<IHostedService>(
                            _ => new DataCollectorService(
                                "HecoChain",
                                "http://13.229.182.155:8545",
                                servicesConfig.MaxParallelism,
                                hecoDataHandler,
                                allAddresses
                            ));
                    }
                });
        }
    }
}
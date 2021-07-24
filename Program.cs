using System;
using System.IO;
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
            ((Hierarchy) LogManager.GetRepository()).Root.Level = Level.Debug;

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
                            _ => new DataCollectorService(
                                "BinanceSmartChain",
                                "http://162.55.98.218:8545",
                                servicesConfig.MaxParallelism,
                                bscDataHandler
                            ));
                    }
/*
                    if (servicesConfig.HecoEnabled)
                        // Create and add the HostedService for Heco Chain
                        services.AddSingleton<IHostedService>(
                            _ => new AnalyzerService(
                                "HecoChain",
                                "http://155.138.154.45:8545",
                                analyzerConfig.HecoEnemies,
                                new TelegramNotifier(
                                    "-516536036"),
                                3,
                                servicesConfig.MaxParallelism,
                                analyzerConfig.HecoAddress,
                                false
                            )
                        );
                        */
                });
        }
    }
}
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

                    if (servicesConfig.PlyPendingEnabled)
                        services.AddSingleton<IHostedService>(
                            _ => new SubscribedService(
                                new TelegramNotifier("-532850503"),
                                "PolygonPending")
                        );

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

                    if (servicesConfig.BscEnabled)
                        // Create and add the HostedService for Binance Smart Chain
                        services.AddSingleton<IHostedService>(
                            _ => new AnalyzerService(
                                "BinanceSmartChain",
                                "http://13.250.53.181:8545",
                                analyzerConfig.BscEnemies,
                                new TelegramNotifier(
                                    "-560874043"),
                                5,
                                servicesConfig.MaxParallelism,
                                analyzerConfig.BscAddress,
                                false
                            )
                        );

                    if (servicesConfig.HecoEnabled)
                        // Create and add the HostedService for Heco Chain
                        services.AddSingleton<IHostedService>(
                            _ => new AnalyzerService(
                                "HecoChain",
                                "http://188.166.254.141:8545",
                                analyzerConfig.HecoEnemies,
                                new TelegramNotifier(
                                    "-516536036"),
                                3,
                                servicesConfig.MaxParallelism,
                                analyzerConfig.HecoAddress,
                                false
                            )
                        );
                });
        }
    }
}

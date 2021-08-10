using System;
using System.Collections.Generic;
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


                        // Poly
                        var plyAllAddresses = analyzerConfig.Ply.Enemies;
                        plyAllAddresses.Add(analyzerConfig.Ply.Address);
                        var plyDataHandler =
                            new DataCollectorService.ChainDataHandler();
                        if (analyzerConfig.Ply.ServicesConfig.AnalyzerService)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new AnalyzerService(
                                    "PolygonChain",
                                    analyzerConfig.Ply.Enemies,
                                    new TelegramNotifier(
                                        "-532850503"),
                                    5,
                                    analyzerConfig.Ply.Address,
                                    plyDataHandler
                                )
                            );
                        }

                        if (analyzerConfig.Ply.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "PolygonChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-532850503"),
                                    chainDataHandler: plyDataHandler,
                                    addressesToCompare: new List<string> {"0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178"},
                                    "ply_tokenlists.data")
                            );
                        }

                        if (analyzerConfig.Ply.ServicesConfig.DataCollector)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new DataCollectorService(
                                    "PolygonChain",
                                    analyzerConfig.Ply.RpcEndpoint,
                                    analyzerConfig.Ply.ServicesConfig.MaxParallelism,
                                    plyDataHandler,
                                    plyAllAddresses
                                ));
                        }


                        // Bsc
                        var bscAllAddresses = analyzerConfig.Bsc.Enemies;
                        bscAllAddresses.Add(analyzerConfig.Bsc.Address);
                        var bscDataHandler =
                            new DataCollectorService.ChainDataHandler();
                        if (analyzerConfig.Bsc.ServicesConfig.AnalyzerService)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new AnalyzerService(
                                    "BinanceSmartChain",
                                    analyzerConfig.Bsc.Enemies,
                                    new TelegramNotifier(
                                        "-560874043"),
                                    5,
                                    analyzerConfig.Bsc.Address,
                                    bscDataHandler
                                )
                            );
                        }

                        if (analyzerConfig.Bsc.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "BinanceSmartChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-560874043"),
                                    chainDataHandler: bscDataHandler,
                                    addressesToCompare: new List<string>
                                    {
                                        "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178",
                                        "0xddafd3baab340b10c19c066ae52f96fe5bee1856",
                                    },
                                    "bsc_tokenlists.data")
                            );
                        }

                        if (analyzerConfig.Bsc.ServicesConfig.DataCollector)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new DataCollectorService(
                                    "BinanceSmartChain",
                                    analyzerConfig.Bsc.RpcEndpoint,
                                    analyzerConfig.Bsc.ServicesConfig.MaxParallelism,
                                    bscDataHandler,
                                    bscAllAddresses
                                ));
                        }

                        // Heco
                        var hecoAllAddresses = analyzerConfig.Heco.Enemies;
                        hecoAllAddresses.Add(analyzerConfig.Heco.Address);
                        var hecoDataHandler =
                            new DataCollectorService.ChainDataHandler();
                        if (analyzerConfig.Heco.ServicesConfig.AnalyzerService)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new AnalyzerService(
                                    "HecoChain",
                                    analyzerConfig.Heco.Enemies,
                                    new TelegramNotifier(
                                        "-516536036"),
                                    5,
                                    analyzerConfig.Heco.Address,
                                    hecoDataHandler
                                )
                            );
                        }

                        if (analyzerConfig.Heco.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "HecoChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-516536036"),
                                    chainDataHandler: hecoDataHandler,
                                    new List<string>
                                    {
                                        "0xa5f2b51aa0fa4be37f372622e28ed5a661802a68",
                                        "0xddafd3baab340b10c19c066ae52f96fe5bee1856"
                                    },
                                    "heco_tokenlists.data")
                            );
                        }

                        if (analyzerConfig.Heco.ServicesConfig.DataCollector)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new DataCollectorService(
                                    "HecoChain",
                                    analyzerConfig.Heco.RpcEndpoint,
                                    analyzerConfig.Heco.ServicesConfig.MaxParallelism,
                                    hecoDataHandler,
                                    hecoAllAddresses
                                ));
                        }
                    }
                );
        }
    }
}
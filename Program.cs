using System;
using System.IO;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using AnalyzerCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            
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
                                        "-532850503",
                                        "1884927181:AAHOZNBdOaTURiZ5-5r669-sIzXUY2ZNiVo"),
                                    5,
                                    analyzerConfig.Ply.Address,
                                    plyDataHandler
                                )
                            );
                        }

<<<<<<< HEAD
                        if (analyzerConfig.Ply.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "PolygonChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-532850503",
                                        "1884927181:AAHOZNBdOaTURiZ5-5r669-sIzXUY2ZNiVo"),
                                    chainDataHandler: plyDataHandler,
                                    addressesToCompare: analyzerConfig.Ply.Enemies,
                                    "polygon_tokenlists.data",
                                    "https://polygonscan.com/")
                            );
                        }

                        if (analyzerConfig.Ply.ServicesConfig.DataCollector)
                        {
                            services.AddScoped<IHostedService>(
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
                            services.AddScoped<IHostedService>(
                                _ => new AnalyzerService(
                                    "BinanceSmartChain",
                                    analyzerConfig.Bsc.Enemies,
                                    new TelegramNotifier(
                                        "-560874043",
                                        "1904993999:AAHxKSPSxPYhmfYOqP1ty11l7Qvts9D0aqk"),
                                    5,
                                    analyzerConfig.Bsc.Address,
                                    bscDataHandler
                                )
                            );
                        }

                        if (analyzerConfig.Bsc.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddScoped<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "BinanceSmartChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-556783420",
                                        "1904993999:AAHxKSPSxPYhmfYOqP1ty11l7Qvts9D0aqk"),
                                    chainDataHandler: bscDataHandler,
                                    addressesToCompare: analyzerConfig.Bsc.Enemies,
                                    "bsc_tokenlists.data",
                                    "https://www.bscscan.com/")
                            );
                        }

                        if (analyzerConfig.Bsc.ServicesConfig.DataCollector)
                        {
                            services.AddScoped<IHostedService>(
                                _ => new DataCollectorService(
                                    "BinanceSmartChain",
                                    analyzerConfig.Bsc.RpcEndpoint,
                                    analyzerConfig.Bsc.ServicesConfig.MaxParallelism,
                                    bscDataHandler,
                                    bscAllAddresses
                                ));
                        }

                        if (analyzerConfig.Bsc.ServicesConfig.NewTokenService)
                        {
                            services.AddHostedService<NewTokenService>(
                                _ => new NewTokenService(
                                    "BinanceSmartChain"
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
                                        "-516536036",
                                        "1932950248:AAEdMVOW5yobVmVicqYXlxqZ2mL1DOeMa-g"),
                                    5,
                                    analyzerConfig.Heco.Address,
                                    hecoDataHandler
                                )
                            );
                        }

                        if (analyzerConfig.Heco.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddScoped<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "HecoChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-516536036",
                                        "1970460018:AAHp9Kfs2RTAYhV_DE45RF6FgRnDYG1lEeg"),
                                    chainDataHandler: hecoDataHandler,
                                    addressesToCompare: analyzerConfig.Heco.Enemies,
                                    "heco_tokenlists.data",
                                    "https://hecoinfo.com/")
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
                        
                        // Ftm
                        var ftmAllAddresses = analyzerConfig.Ftm.Enemies;
                        ftmAllAddresses.Add(analyzerConfig.Ftm.Address);
                        var ftmDataHandler =
                            new DataCollectorService.ChainDataHandler();
                        if (analyzerConfig.Ftm.ServicesConfig.AnalyzerService)
                        {
                            services.AddScoped<IHostedService>(
                                _ => new AnalyzerService(
                                    "FantomChain",
                                    analyzerConfig.Ftm.Enemies,
                                    new TelegramNotifier(
                                        "-516536036",
                                        "1932950248:AAEdMVOW5yobVmVicqYXlxqZ2mL1DOeMa-g"),
                                    5,
                                    analyzerConfig.Ftm.Address,
                                    ftmDataHandler
                                )
                            );
                        }

                        if (analyzerConfig.Ftm.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddScoped<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "FantomChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-516536036",
                                        "1932950248:AAEdMVOW5yobVmVicqYXlxqZ2mL1DOeMa-g"),
                                    chainDataHandler: ftmDataHandler,
                                    addressesToCompare: analyzerConfig.Ftm.Enemies,
                                    "fantom_tokenlists.data",
                                    "https://ftmscan.com/")
                            );
                        }

                        if (analyzerConfig.Ftm.ServicesConfig.DataCollector)
                        {
                            services.AddScoped<IHostedService>(
                                _ => new DataCollectorService(
                                    "FtmChain",
                                    analyzerConfig.Ftm.RpcEndpoint,
                                    analyzerConfig.Ftm.ServicesConfig.MaxParallelism,
                                    ftmDataHandler,
                                    ftmAllAddresses
                                ));
                        }
                    }
                );
=======
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
>>>>>>> main
        }
    }
}

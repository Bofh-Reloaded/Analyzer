using System;
using System.Collections.Generic;
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

                        if (analyzerConfig.Ply.ServicesConfig.TokenAnalyzer)
                        {
                            services.AddSingleton<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "PolygonChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-532850503",
                                        "1884927181:AAHOZNBdOaTURiZ5-5r669-sIzXUY2ZNiVo"),
                                    chainDataHandler: plyDataHandler,
                                    addressesToCompare: new List<string>
                                        { "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178" },
                                    "ply_tokenlists.data",
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
                                        "-560874043",
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
                            services.AddSingleton<IHostedService>(
                                _ => new TokenObserverService(
                                    chainName: "HecoChain",
                                    telegramNotifier: new TelegramNotifier(
                                        "-516536036",
                                        "1932950248:AAEdMVOW5yobVmVicqYXlxqZ2mL1DOeMa-g"),
                                    chainDataHandler: hecoDataHandler,
                                    new List<string>
                                    {
                                        "0xa5f2b51aa0fa4be37f372622e28ed5a661802a68"
                                    },
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
                    }
                );
        }
    }
}
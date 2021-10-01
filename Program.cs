using System;
using System.IO;
using AnalyzerCore.Models;
using AnalyzerCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;
using AnalyzerService = AnalyzerCore.Services.AnalyzerService;

namespace AnalyzerCore
{
    internal static class Program
    {
        private const string Version = "0.9.2-db-persistance-websocket";

        private static void Main(string[] args)
        {
            var log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
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

                    var plyDataHandler =
                        new DataCollectorService.ChainDataHandler();
                    if (analyzerConfig.ServicesConfig.AnalyzerService.Enabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new AnalyzerService(analyzerConfig, plyDataHandler, Version)
                        );
                    }

                    if (analyzerConfig.ServicesConfig.TokenAnalyzer.Enabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new TokenObserverService(analyzerConfig, Version)
                        );
                    }

                    if (analyzerConfig.ServicesConfig.DataCollector.Enabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new DataCollectorService(analyzerConfig, plyDataHandler, Version)
                        );
                    }
                });
        }
    }
}
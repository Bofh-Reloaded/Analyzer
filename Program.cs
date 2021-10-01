using System;
using System.IO;
using CommandLine;
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
        private const string TASK_VERSION = "0.9.3-db-persistance-websocket";

        private static string _configFileName;

        private class Options
        {
            [Option('c', "config", Required = true, HelpText = "config file to load")]
            public string Config { get; set; }
        }

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

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    if (o.Config.Length > 0)
                    {
                        _configFileName = o.Config;
                    }
                });

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
                        .AddJsonFile(_configFileName, false, true)
                        .Build();

                    // Map json configuration inside Object
                    var section = configuration.GetSection(nameof(AnalyzerConfig));
                    var analyzerConfig = section.Get<AnalyzerConfig>();

                    var plyDataHandler =
                        new DataCollectorService.ChainDataHandler();
                    if (analyzerConfig.ServicesConfig.AnalyzerService.Enabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new AnalyzerService(analyzerConfig, plyDataHandler, TASK_VERSION)
                        );
                    }

                    if (analyzerConfig.ServicesConfig.TokenAnalyzer.Enabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new TokenObserverService(analyzerConfig, TASK_VERSION)
                        );
                    }

                    if (analyzerConfig.ServicesConfig.DataCollector.Enabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new DataCollectorService(analyzerConfig, plyDataHandler, TASK_VERSION)
                        );
                    }
                });
        }
    }
}
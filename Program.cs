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
        private const string TaskVersion = "0.9.5-db-persistance-websocket";

        private static string _configFileName;
        private static bool _statsEnabled = false;
        private static bool _tokenAnalyzerEnabled = false;
        private static bool _porcoDioEnabled = false;
        private const LogEventLevel TASK_LOG_EVENT_LEVEL = LogEventLevel.Information;

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

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    if (o.Config.Length > 0)
                    {
                        _configFileName = o.Config;
                    }

                    _statsEnabled = o.Stats;
                    _tokenAnalyzerEnabled = o.Token;
                    _porcoDioEnabled = o.Porco;

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

                    var dataHandler =
                        new DataCollectorService.ChainDataHandler();
                    if (_statsEnabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new AnalyzerService(analyzerConfig, dataHandler, TaskVersion)
                        );
                    }

                    if (_tokenAnalyzerEnabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new TokenObserverService(analyzerConfig, TaskVersion)
                        );
                    }

                    if (_statsEnabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new DataCollectorService(analyzerConfig, dataHandler, TaskVersion, TASK_LOG_EVENT_LEVEL)
                        );
                    }

                    if (_porcoDioEnabled)
                    {
                        services.AddScoped<IHostedService>(
                            _ => new PorcoDioService(analyzerConfig, TaskVersion)
                        );
                    }
                });
        }

        private class Options
        {
            [Option('c', "config", Required = true, HelpText = "config file to load")]
            public string Config { get; set; }
            
            [Option('s', "stats", Required=false, HelpText = "Start Stats Service")]
            public bool Stats { get; set; }
            
            [Option('t', "token", Required = false, HelpText = "Start Token Analyzer Service")]
            public bool Token { get; set; }
            
            [Option('k', "porco", Required = false, HelpText = "Porcodidio")]
            public bool Porco { get; set; }
        }
    }
}
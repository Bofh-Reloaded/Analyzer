using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using AnalyzerCore.Application.Tokens.Commands.CreateToken;
using AnalyzerCore.Domain.Models;
using AnalyzerCore.Domain.Repositories;
using AnalyzerCore.Domain.Services;
using AnalyzerCore.Infrastructure.BackgroundServices;
using AnalyzerCore.Infrastructure.Blockchain;
using AnalyzerCore.Infrastructure.Notifications;
using AnalyzerCore.Infrastructure.Persistence;
using AnalyzerCore.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nethereum.Web3;
using Serilog;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                                  "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Starting AnalyzerCore");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", 
                            optional: true, 
                            reloadOnChange: true)
                        .AddEnvironmentVariables()
                        .AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    // Configuration
                    var chainConfig = hostContext.Configuration
                        .GetSection("ChainConfig")
                        .Get<ChainConfig>();
                    services.AddSingleton(chainConfig);

                    // Database
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseSqlite(
                            hostContext.Configuration.GetConnectionString("DefaultConnection"),
                            b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

                    // Blockchain
                    services.AddSingleton<Web3>(provider =>
                    {
                        var config = provider.GetRequiredService<ChainConfig>();
                        return new Web3($"http://{config.RpcUrl}:{config.RpcPort}");
                    });
                    services.AddScoped<IBlockchainService, BlockchainService>();

                    // Repositories
                    services.AddScoped<ITokenRepository, TokenRepository>();
                    services.AddScoped<IPoolRepository, PoolRepository>();

                    // Notifications
                    services.AddHttpClient<INotificationService, TelegramNotificationService>()
                        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = 
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        });

                    services.AddSingleton<INotificationService>(provider =>
                    {
                        var config = hostContext.Configuration.GetSection("Telegram");
                        var httpClient = provider.GetRequiredService<HttpClient>();
                        var logger = provider.GetRequiredService<ILogger<TelegramNotificationService>>();
                        return new TelegramNotificationService(
                            httpClient,
                            logger,
                            config["BotToken"],
                            config["ChatId"]);
                    });

                    // MediatR
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateTokenCommand).Assembly));

                    // Background Services
                    services.AddHostedService<BlockchainMonitorService>();

                    // Logging
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddSerilog(dispose: true);
                    });
                });
    }
}
using AnalyzerCore.Application.Pools.Commands.CreatePool;
using AnalyzerCore.Domain.Models;
using AnalyzerCore.Domain.Repositories;
using AnalyzerCore.Domain.Services;
using AnalyzerCore.Infrastructure.BackgroundServices;
using AnalyzerCore.Infrastructure.Blockchain;
using AnalyzerCore.Infrastructure.Persistence;
using AnalyzerCore.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nethereum.Web3;
using Serilog;
using Serilog.Events;

namespace AnalyzerCore.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup failed");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    var configuration = hostContext.Configuration;

                    // Configuration
                    var chainConfig = configuration
                        .GetSection("ChainConfig")
                        .Get<ChainConfig>();
                    services.AddSingleton(chainConfig);

                    // Database
                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        var connectionString = configuration.GetConnectionString("DefaultConnection");
                        if (string.IsNullOrEmpty(connectionString))
                        {
                            throw new InvalidOperationException("Database connection string is not configured");
                        }
                        options.UseSqlite(connectionString);
                    });

                    // Blockchain
                    services.AddSingleton<Web3>(provider =>
                    {
                        var config = provider.GetRequiredService<ChainConfig>();
                        var url = $"https://{config.RpcUrl}";
                        return new Web3(url);
                    });
                    services.AddScoped<IBlockchainService, BlockchainService>();

                    // Repositories
                    services.AddScoped<ITokenRepository, TokenRepository>();
                    services.AddScoped<IPoolRepository, PoolRepository>();

                    // MediatR
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreatePoolCommand).Assembly));

                    // Background Services
                    services.AddHostedService<BlockchainMonitorService>();
                });
    }
}
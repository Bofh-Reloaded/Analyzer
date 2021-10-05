using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.DbLayer;
using AnalyzerCore.Models;
using Microsoft.Extensions.Configuration;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace AnalyzerCore.Services
{
    public class NewTokenService : BackgroundService
    {
        private static readonly TelegramBotClient Bot =
            new TelegramBotClient("1904993999:AAHxKSPSxPYhmfYOqP1ty11l7Qvts9D0aqk");

        private static readonly string TelegramChatId = "-502311043";
        private static readonly string TokenFileName = "bsc_tokenlists.data";
        private readonly string _chainName;
        private readonly TokenDbContext _db;
        private readonly Logger _log;
        private readonly string _version;
        private readonly string _wssUri;

        public NewTokenService(string chainName, string version)
        {
            _chainName = chainName;
            _wssUri = "wss://bsc-ws-node.nariox.org:443";
            _version = version;
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            LogContext.PushProperty("SourceContext", $"{_chainName}");
            _db = new TokenDbContext();
            _log.Information("Starting Service");
        }

        private static string NewPairEvent { get; } =
            "0x0d3648bd0f6ba80134a33ba9275ac585d9d315f0ad8355cddefde31afa28d0e9";

        private static string GetLinkFromElement(string element, string type = "token")
        {
            var baseUri = $"https://bscscan.com/{type}";
            return $"<a href='{baseUri}/{element}'>{element[..10]}...{element[^10..]}</a>{Environment.NewLine}";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information($"Starting cycle, wss: {_wssUri}");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Load configuration regarding tokens
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                    .AddJsonFile(TokenFileName, false, true)
                    .Build();
                var tokenList = configuration.Get<TokenListConfig>();
                tokenList.blacklisted ??= new List<string>();
                using var client = new StreamingWebSocketClient("ws://195.201.169.14:8546");
                // create a log filter specific to Transfers
                // this filter will match any Transfer (matching the signature) regardless of address
                var filterTransfers = Event<PairCreatedEvent>.GetEventABI().CreateFilterInput();
                filterTransfers.Topics[0] = NewPairEvent;

                // create the subscription
                // it won't do anything yet
                var subscription = new EthLogsObservableSubscription(client);

                subscription.GetSubscribeResponseAsObservable()
                    .Subscribe(s => _log.Information($"Subscription Id: {s}"));

                // attach a handler for Transfer event logs
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(async log =>
                {
                    try
                    {
                        _log.Information("Topics: " + log.GetTopic(0) + " txhash: " + log.TransactionHash);
                        if (log.GetTopic(0) != NewPairEvent) return;
                        var token1 = log.Topics[1].ToString()?.Replace("0x000000000000000000000000", "0x");
                        var token2 = log.Topics[2].ToString()?.Replace("0x000000000000000000000000", "0x");
                        var pool = log.Data.Replace("0x000000000000000000000000", "0x")[..42];
                        var token1Flag = false;
                        var token2Flag = false;
                        if (token1 == null || token2 == null) return;
                        if (tokenList.whitelisted.Contains(token1.ToLower()) ||
                            tokenList.blacklisted.Contains(token1.ToLower())) token1Flag = true;
                        if (tokenList.whitelisted.Contains(token2.ToLower()) ||
                            tokenList.blacklisted.Contains(token2.ToLower())) token2Flag = true;
                        if (token1Flag && token2Flag) return;
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                        var web3 = new Web3("http://136.243.15.134:8545");
                        var token1ContractHandler = web3.Eth.GetContractHandler(token1);
                        var token2ContractHandler = web3.Eth.GetContractHandler(token2);
                        var poolContractHandler = web3.Eth.GetContractHandler(pool);
                        var token1Symbol = token1ContractHandler.QueryAsync<SymbolFunction, string>();
                        token1Symbol.Wait(stoppingToken);
                        var token2Symbol = token2ContractHandler.QueryAsync<SymbolFunction, string>();
                        token2Symbol.Wait(stoppingToken);
                        var poolSymbol = poolContractHandler.QueryAsync<SymbolFunction, string>();
                        poolSymbol.Wait(stoppingToken);
                        var exchange = poolContractHandler.QueryAsync<FactoryFunction, string>();
                        exchange.Wait(stoppingToken);
                        var exchangeContractHandler = web3.Eth.GetContractHandler(exchange.Result);
                        await Bot.SendTextMessageAsync(
                            chatId: TelegramChatId,
                            text: $"<b>New PairCreated Event</b>{Environment.NewLine}" +
                                  $"Token1: <b>[{token1Symbol.Result}]:</b> {GetLinkFromElement(token1)}" +
                                  $"Token2: <b>[{token2Symbol.Result}]:</b> {GetLinkFromElement(token2)}" +
                                  $"Pool: <b>[{poolSymbol.Result}]:</b> {GetLinkFromElement(pool)}" +
                                  $"Exchange: <b>[]:</b> {GetLinkFromElement(exchange.Result, "address")}",
                            parseMode: ParseMode.Html,
                            disableWebPagePreview: true, cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("{Error}", ex.ToString());
                    }
                });

                // open the web socket connection
                await client.StartAsync();

                // begin receiving subscription data
                // data will be received on a background thread
                await subscription.SubscribeAsync(filterTransfers);

                while (subscription.SubscriptionState is SubscriptionState.Subscribing or SubscriptionState.Subscribed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
    }
}
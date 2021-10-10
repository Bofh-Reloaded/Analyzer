using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using Newtonsoft.Json;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AnalyzerCore.Services
{
    public class PorcoDioService : BackgroundService
    {
        private readonly Logger _log;
        private readonly AnalyzerConfig _config;
        private readonly string _version;
        private readonly Web3 _web3;
        private readonly Dictionary<string, List<TokenTraded>> _data = new();


        public class TokenTraded
        {
            public string TokenAddress { get; set; }
            public string TokenSymbol { get; set; }
            public int CountedTransactions { get; set; }
        }

        public string Address { get; set; }
        public List<TokenTraded> TokensTraded = new();


        private readonly List<string> _addressesToAnalyze = new()
        {
            "0xaa7dcd270dd6734db602c3ea2e9749f74486619d"
        };

        private readonly string _chainName;

        public PorcoDioService(AnalyzerConfig config, string version)
        {
            _chainName = config.ChainName;
            _config = config;
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
            _web3 = new Web3($"http://{config.RpcEndpoints.First()}:{config.RpcPort.ToString()}");
            var f = System.IO.File.ReadAllText("test.json");
            _data = JsonSerializer.Deserialize<Dictionary<string, List<TokenTraded>>>(f);
            _log.Information("Starting Service");
        }

        private async Task ProcessNewBlock(Block block, CancellationToken cancellationToken)
        {
            BlockWithTransactions blockTransactions;
            try
            {
                blockTransactions = await
                    _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                        new BlockParameter((ulong)block.Number.Value));
            }
            catch (Exception e)
            {
                _log.Error("{Message}", e.Message);
                return;
            }

            var transactionToAddressInterested =
                blockTransactions.Transactions.Where(t => t.To == _addressesToAnalyze.First()).ToList();
            _log.Information("{Block}: {NumberOfTrx}", block.Number.Value, transactionToAddressInterested.Count());
            foreach (var transaction in transactionToAddressInterested)
            {
                if (!_data.ContainsKey(transaction.From))
                {
                    _data[transaction.From] = new List<TokenTraded>();
                }

                _log.Information("element in data: {NumberOfElements}", _data.Count.ToString());
                _log.Information("from address: {Address}, txhash: {TxHash}", transaction.From, transaction.TransactionHash);

                var transactionReceipt =
                    await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction.TransactionHash);
                foreach (var log in transactionReceipt.Logs)
                {
                    if (log["topics"][0].ToString().ToLower() ==
                        "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1")
                    {
                        _log.Information("found sync event (first pool used)");
                        var contractHandler = _web3.Eth.GetContractHandler(log["address"].ToString());
                        var tokens = await GetTokensFromPool(contractHandler);
                        var tokensDictionary = new Dictionary<string, string>();
                        foreach (var t in tokens)
                        {
                            var tokenContractHandler = _web3.Eth.GetContractHandler(t);
                            var tokenSymbol = await tokenContractHandler.QueryAsync<SymbolFunction, string>();
                            tokensDictionary[t] = tokenSymbol;
                        }

                        foreach (var t in tokensDictionary.Distinct())
                        {
                            if (_data[transaction.From].Exists(token => token.TokenAddress == t.Key))
                            {
                                var e = _data[transaction.From].Find(token => token.TokenAddress == t.Key);
                                if (e is not null)
                                {
                                    // ReSharper disable once PossibleNullReferenceException
                                    _data[transaction.From].Find(token => token.TokenAddress == t.Key)
                                        .CountedTransactions++;
                                }
                            }
                            else
                            {
                                var tokenTraded = new TokenTraded();
                                tokenTraded.CountedTransactions = 1;
                                tokenTraded.TokenAddress = t.Key;
                                tokenTraded.TokenSymbol = t.Value;
                                _data[transaction.From].Add(tokenTraded);
                            }
                        }
                        _log.Information("found following tokens: {Tokens}", string.Join(",", tokensDictionary));
                    }
                }
            }

            var jsonString = JsonSerializer.Serialize(_data,
                new JsonSerializerOptions() { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync("test.json", jsonString, cancellationToken);
        }

        private static async Task<List<string>> GetTokensFromPool(ContractHandler poolContractHandler)
        {
            try
            {
                var tokens = new List<string>();

                var token0OutputDto =
                    await poolContractHandler.QueryDeserializingToObjectAsync<Token0Function, Token0OutputDTO>();

                tokens.Add(token0OutputDto.ReturnValue1);

                var token1OutputDto =
                    await poolContractHandler.QueryDeserializingToObjectAsync<Token1Function, Token1OutputDTO>();

                tokens.Add(token1OutputDto.ReturnValue1);
                return tokens;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var uri = $"ws://{_config.RpcEndpoints.First()}:{_config.WssPort.ToString()}";
                _log.Information("Using WebSocket on: {Uri}", uri);
                using var client = new StreamingWebSocketClient(uri);
                // create the subscription
                // (it won't start receiving data until Subscribe is called)
                var subscription = new EthNewBlockHeadersObservableSubscription(client);

                // attach a handler for when the subscription is first created (optional)
                // this will occur once after Subscribe has been called
                subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                    Console.WriteLine("Block Header subscription Id: " + subscriptionId));

                DateTime? lastBlockNotification = null;
                double secondsSinceLastBlock = 0;

                // attach a handler for each block
                // put your logic here
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(async block =>
                {
                    secondsSinceLastBlock = (lastBlockNotification == null)
                        ? 0
                        : (int)DateTime.Now.Subtract(lastBlockNotification.Value).TotalSeconds;
                    lastBlockNotification = DateTime.Now;
                    var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value);
                    _log.Warning(
                        "New Block. Number: {Block}, Timestamp UTC: {TimeStamp}, Seconds since last block received: {SecondsSinceLastBlock}",
                        block.Number.Value,
                        JsonConvert.SerializeObject(utcTimestamp.ToString()),
                        secondsSinceLastBlock);
                    await ProcessNewBlock(block, stoppingToken);
                });

                // handle unsubscription
                // optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    _log.Warning("Block Header unsubscribe result: {Response}", response);
                });

                // open the websocket connection
                await client.StartAsync();

                // start the subscription
                // this will only block long enough to register the subscription with the client
                // once running - it won't block whilst waiting for blocks
                // blocks will be delivered to our handler on another thread
                await subscription.SubscribeAsync();

                while (subscription.SubscriptionState is SubscriptionState.Subscribed or SubscriptionState.Subscribing)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    _log.Warning("{SubscriptionState}", subscription.SubscriptionState);
                    secondsSinceLastBlock = (lastBlockNotification == null)
                        ? 0
                        : (int)DateTime.Now.Subtract(lastBlockNotification.Value).TotalSeconds;
                    _log.Warning("Last block notification: {SecondsSinceLastBlock} seconds ago", secondsSinceLastBlock);
                    if (!(secondsSinceLastBlock > 20)) continue;
                    _log.Error("Websocket streaming bugged, restarting...");
                    await subscription.UnsubscribeAsync();
                }
            }
        }
    }
}
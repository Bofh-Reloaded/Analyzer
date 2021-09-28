#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.DbLayer;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using Serilog;
using Microsoft.Extensions.Configuration;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Serilog.Context;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore.Services
{
    public class TokenObserverService : BackgroundService
    {
        private const string TASK_VERSION = "0.9.1-db-persistance-websocket";

        private const string TASK_SYNC_EVENT_ADDRESS =
            "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        private const int TASK_TASK_DELAY_MS = 60000;
        private readonly string _baseUri;
        private readonly string _chainName;

        private readonly Serilog.Core.Logger _log;
        private readonly TelegramNotifier _telegramNotifier;
        private readonly List<string> _tokenAddressToCompareWith;

        private readonly string _tokenFileName;

        private readonly Web3 _web3;

        // Initialize configuration accessor
        private IConfigurationRoot? _configuration;

        private TokenListConfig _tokenList = null!;
        private readonly AnalyzerConfig _config;


        public TokenObserverService(AnalyzerConfig config)
        {
            _chainName = config.ChainName;
            _telegramNotifier = new TelegramNotifier(config.ServicesConfig.TokenAnalyzer.ChatId,
                config.ServicesConfig.TokenAnalyzer.BotToken);
            _tokenAddressToCompareWith = config.Enemies;
            _baseUri = config.ServicesConfig.TokenAnalyzer.BaseUri;
            _web3 = new Web3($"http://{config.RpcEndpoints.First()}:{config.RpcPort.ToString()}");
            _tokenFileName = config.ServicesConfig.TokenAnalyzer.TokenFileName;
            _config = config;

            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    LogEventLevel.Debug,
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            LogContext.PushProperty("SourceContext", $"{_chainName}");
        }

        private async Task CleanUpTelegram(TokenDbContext context,
            ICollection<string> knownToken,
            TelegramNotifier telegramNotifier)
        {
            // Iterate all Notified Tokens
            var notifiedTokens = context.Tokens
                .Where(o => o.Notified == true && o.Deleted == false);
            foreach (var token in notifiedTokens)
            {
                if (!knownToken.Contains(token.TokenAddress)) continue;
                _log.Information($"Deleting messageId: {token.TelegramMsgId}...");
                await telegramNotifier.DeleteMessageAsync(token.TelegramMsgId);
                token.Deleted = true;
                await context.SaveChangesAsync();
            }
        }

        private Task<TransactionReceipt> GetTransactionReceipt(string txHash)
        {
            var result = _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            result.Wait();
            if (result == null) throw new NullReferenceException();
            return result;
        }

        private IEnumerable<JToken>? GetPoolUsedFromTransaction(Transaction enT)
        {
            var policy = Policy.Handle<Exception>()
                .Retry(3, onRetry: (exception, retryCount, context) =>
                {
                    _log.Error("Cannot retrieve: _web3.Eth.Transactions.GetTransactionReceipt");
                });
            var result = policy.Execute(
                () => GetTransactionReceipt(enT.TransactionHash)
            );
            
            var syncEventsInLogs = result.Result.Logs.Where(
                e => string.Equals(e["topics"][0].ToString().ToLower(),
                    TASK_SYNC_EVENT_ADDRESS, StringComparison.Ordinal)
            ).ToList();

            return syncEventsInLogs.Count == 0
                ? new List<JToken>()
                : syncEventsInLogs.Select(pool => pool["address"])
                    .ToList();
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

        private void ProcessTokenAndPersistInDb(string token, string tokenSymbol, string txHash, string poolAddress,
            string exchangeAddress, Transaction t)
        {
            try
            {
                _log.Information($"EvaluateToken: {token} with txHash: {txHash}");
                using var db = new TokenDbContext();
                // If the token is not present in the database, let's add it.
                var tokenToCheck = db.Tokens.FirstOrDefault(tokenEntity => tokenEntity.TokenAddress == token);
                if (tokenToCheck != null)
                {
                    // UPDATE TOKEN
                    // If we already seen the transaction hash, don't do nothing, we are probably reading the same
                    // block twice (very hardly).
                    var toCheck = db.Tokens.FirstOrDefault(k => k.TokenAddress == token);
                    if (toCheck?.TransactionHashes.FirstOrDefault(k => k.Hash == txHash) !=
                        null)
                    {
                        _log.Information($"We already seen token: {token} within txHash: {txHash}, skipping...");
                        return;
                    }

                    // Update token details if we haven't seen that trx yet
                    var entity = db.Tokens.FirstOrDefault(tokenEntity => tokenEntity.TokenAddress == token);
                    entity?.TransactionHashes.Add(new DbLayer.Models.TransactionHash()
                    {
                        Hash = txHash
                    });
                    if (entity != null)
                    {
                        entity.TxCount = entity.TxCount + 1;
                        entity.From = t.From;
                        entity.To = t.To;
                        db.Entry(entity).Collection(c => c.Exchanges).Load();
                        db.Entry(entity).Collection(c => c.Pools).Load();
                        if (entity.Exchanges.FirstOrDefault(e => e.Address == exchangeAddress.ToLower()) == null)
                            entity.Exchanges.Add(new DbLayer.Models.Exchange() { Address = exchangeAddress.ToLower() });
                        if (entity.Pools.FirstOrDefault(p => p.Address == poolAddress.ToLower()) == null)
                            entity.Pools.Add(new DbLayer.Models.Pool() { Address = poolAddress.ToLower() });
                    }
                    else
                    {
                        _log.Error($"Error retrieving entity for Token: {token}, database is not update");
                    }

                    db.SaveChanges();
                }
                else
                {
                    // CREATE TOKEN
                    // Create the object inside the dictionary since it's the first time that we see it
                    db.Tokens.Add(new DbLayer.Models.TokenEntity
                    {
                        TokenAddress = token,
                        TokenSymbol = tokenSymbol,
                        From = t.From,
                        To = t.To,
                        IsDeflationary = false,
                        TxCount = 1,
                        Notified = false,
                        TelegramMsgId = -1
                    });
                    db.SaveChanges();
                    var entity = db.Tokens.FirstOrDefault(e => e.TokenAddress == token);
                    entity?.Exchanges.Add(new DbLayer.Models.Exchange()
                    {
                        Address = exchangeAddress.ToLower()
                    });
                    entity?.Pools.Add(new DbLayer.Models.Pool()
                    {
                        Address = poolAddress.ToLower()
                    });
                    entity?.TransactionHashes.Add(new DbLayer.Models.TransactionHash()
                    {
                        Hash = t.TransactionHash
                    });
                    db.SaveChanges();
                }

                var totalExchangesFound = db.Tokens.Where(entity => entity.TokenAddress == token)
                    .Select(e => e.Exchanges).ToList().Count;
                var totalTransactionsCount = db.Tokens.Where(entity => entity.TokenAddress == token)
                    .Select(e => e.TxCount).FirstOrDefault();
                _log.Information(
                    $"Found missing Token: {token} with TxHash: {txHash} with {totalExchangesFound} pools, total txCount: {totalTransactionsCount}");
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
                throw;
            }
        }

        private async Task NotifyMissingTokens()
        {
            await using var context = new TokenDbContext();
            var query = context.Tokens.Where(t => t.Notified == false);
            var tokenToNotify = query.ToList();
            _log.Debug($"MissingTokens: {tokenToNotify.ToList().Count.ToString()}");
            if (tokenToNotify.ToList().Count <= 0)
            {
                _log.Information("No Missing token found this time");
                return;
            }

            foreach (var t in tokenToNotify)
            {
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.Exchanges)
                    .LoadAsync();
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.Pools)
                    .LoadAsync();
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.TransactionHashes)
                    .LoadAsync();
                const string star = "\U00002B50";
                var transactionHash = t.TransactionHashes.FirstOrDefault()?.Hash;
                var pools = t.Pools.ToList();
                if (transactionHash != null)
                {
                    var msg = string.Join(
                        Environment.NewLine,
                        $"<b>{t.TokenSymbol} [<a href='{_baseUri}token/{t.TokenAddress}'>{t.TokenAddress}</a>] {string.Concat(Enumerable.Repeat(star, t.TxCount))}:</b>",
                        $"  token address: {t.TokenAddress}",
                        $"  totalSupplyChanged: {t.IsDeflationary.ToString()}",
                        $"  totalTxCount: {t.TxCount.ToString()}",
                        $"  lastTxSeen: <a href='{_baseUri}tx/{transactionHash}'>{transactionHash[..10]}...{transactionHash[^10..]}</a>",
                        $"  from: <a href='{_baseUri}{t.From}'>{t.From[..10]}...{t.From[^10..]}</a>",
                        $"  to: <a href='{_baseUri}{t.To}'>{t.To[..10]}...{t.To[^10..]}</a>",
                        $"  pools: [{Environment.NewLine}{string.Join(Environment.NewLine, pools.Select(p => $"    <a href='{_baseUri}address/{p.Address.ToString()}'>{p.Address.ToString()[..10]}...{p.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  exchanges: [{Environment.NewLine}{string.Join(Environment.NewLine, t.Exchanges.Select(e => $"    <a href='{_baseUri}address/{e.Address.ToString()}'>{e.Address.ToString()[..10]}...{e.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  version: {TASK_VERSION}"
                    );

                    _log.Information(JsonConvert.SerializeObject(msg, Formatting.Indented));
                    var notifierResponse = await _telegramNotifier.SendMessageWithReturnAsync(msg);

                    t.Notified = true;
                    if (notifierResponse != null) t.TelegramMsgId = notifierResponse.MessageId;
                }

                await context.SaveChangesAsync();
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information($"Starting TokenObserverService for chain: {_chainName} with version: {TASK_VERSION}");
            _telegramNotifier.SendMessage(
                $"Starting TokenObserverService for chain: {_chainName} with version: {TASK_VERSION}");
            stoppingToken.Register(() =>
                {
                    _log.Information($"TokenObserverService background task is stopping for chain: {_chainName}");
                }
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                var uri = $"ws://{_config.RpcEndpoints.First()}:{_config.WssPort}";
                _log.Information($"Using WebSocket on: {uri}");
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
                        $"New Block. Number: {block.Number.Value}, Timestamp UTC: {JsonConvert.SerializeObject(utcTimestamp)}, Seconds since last block received: {secondsSinceLastBlock} ");
                    await ProcessNewBlock(block, stoppingToken);
                });

                var subscribed = true;

                // handle unsubscription
                // optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    subscribed = false;
                    _log.Warning("Block Header unsubscribe result: " + response);
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
                    _log.Warning(subscription.SubscriptionState.ToString());
                    secondsSinceLastBlock = (lastBlockNotification == null)
                        ? 0
                        : (int)DateTime.Now.Subtract(lastBlockNotification.Value).TotalSeconds;
                    _log.Warning($"Last block notification: {secondsSinceLastBlock} seconds ago");
                    if (!(secondsSinceLastBlock > 20)) continue;
                    _log.Error("Websocket streaming bugged, restarting...");
                    await subscription.UnsubscribeAsync();
                    //await StopAsync(new CancellationToken());
                    //await StartAsync(new CancellationToken());
                }
            }
        }

        private List<string> ReloadTokenList()
        {
            try
            {
                // Load configuration regarding tokens
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                    .AddJsonFile(_tokenFileName, false, true)
                    .Build();
            }
            catch (Exception)
            {
                _log.Error("Cannot load token file... skipping cycle");
                _telegramNotifier.SendMessage(
                    $"<b>Token file not present: {_tokenFileName}... I cannot check tokens \U0001F62D\U0001F62D\U0001F62D</b>");
            }

            _tokenList = _configuration.Get<TokenListConfig>();
            _tokenList.blacklisted ??= new List<string>();
            return _tokenList.whitelisted.Concat(_tokenList.blacklisted).ToList();
        }

        private async Task ProcessNewBlock(Block block, CancellationToken cancellationToken)
        {
            _log.Information($"New Block Received: {block.Number.Value}");

            List<string> completeTokenList = ReloadTokenList();

            // Retrieve Transactions inside the block
            _log.Information("getting transactions inside block");
            /*var policy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3)
                });
            Task<BlockWithTransactions> blockTransactions = null;
            await policy.ExecuteAsync(async () =>
            {
                blockTransactions =
                    _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                        new BlockParameter((ulong)block.Number.Value));
                try
                {
                    blockTransactions.Wait(cancellationToken);
                }
                catch (Exception)
                {
                    blockTransactions = null;
                }
            });*/
            BlockWithTransactions blockTransactions = null;
            try
            {
                blockTransactions = await
                    _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                        new BlockParameter((ulong)block.Number.Value));
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
                return;
            }

            _log.Information("transactions retrieved");

            // Slow down...
            if (blockTransactions != null)
            {
                _log.Information(
                    $"Transaction inside block [{block.Number.Value}]: {blockTransactions.Transactions.Length}");

                // Select only transaction from the address that we need analyze
                var transactionsToAnalyze = new List<Transaction>();
                foreach (var tx in blockTransactions.Transactions)
                {
                    var fromAddr = tx.From?.ToLower();
                    var toAddr = tx.To?.ToLower();
                    if (fromAddr != null && _tokenAddressToCompareWith.Contains(fromAddr))
                        transactionsToAnalyze.Add(tx);
                    if (toAddr != null && _tokenAddressToCompareWith.Contains(toAddr)) transactionsToAnalyze.Add(tx);
                }

                var enTransactions = transactionsToAnalyze.ToList();
                _log.Debug($"Total transaction to analyze: {enTransactions.Count().ToString()}");
                foreach (var t in enTransactions)
                {
                    IEnumerable<JToken>? poolsUsed = null;
                    while (poolsUsed == null)
                    {
                        try
                        {
                            poolsUsed = GetPoolUsedFromTransaction(t);
                        }
                        catch (AggregateException)
                        {
                            poolsUsed = null;
                        }
                    }
                    foreach (var poolContractHandler in poolsUsed.Select(pool =>
                        _web3.Eth.GetContractHandler(pool.ToString())))
                    {
                        try
                        {
                            List<string> tokens = await GetTokensFromPool(poolContractHandler);
                            if (tokens.Count <= 0) return;
                            var poolFactory = poolContractHandler.QueryAsync<FactoryFunction, string>();
                            poolFactory.Wait(cancellationToken);
                            foreach (var token in tokens)
                            {
                                await using var db = new TokenDbContext();
                                // Skip this token if we already have it
                                if (completeTokenList.Contains(token))
                                {
                                    _log.Debug($"Token: {token} already known.");
                                    continue;
                                }

                                _log.Debug($"[ ] Token: {token}");
                                var tokenContractHandler = _web3.Eth.GetContractHandler(token);
                                var tokenSymbol = await tokenContractHandler.QueryAsync<SymbolFunction, string>();
                                var tokenTotalSupply = await tokenContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>();
                                _log.Debug(
                                    $"[  ] {tokenSymbol} {tokenTotalSupply.ToString()} {t.TransactionHash} {poolFactory.Result}");
                                ProcessTokenAndPersistInDb(
                                    token,
                                    tokenSymbol,
                                    t.TransactionHash,
                                    poolContractHandler.ContractAddress,
                                    poolFactory.Result,
                                    t);
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(e.ToString());
                        }
                    }
                }
            }

            _log.Information("Analysis complete");
            await NotifyMissingTokens();
            _log.Information("Cleaning Tokens");
            // Clean up Telegram
            await CleanUpTelegram(new TokenDbContext(), completeTokenList, _telegramNotifier);
        }
    }
}
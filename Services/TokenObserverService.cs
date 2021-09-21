#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.DbLayer;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using Serilog;
using Microsoft.Extensions.Configuration;
using Nethereum.Contracts.ContractHandlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog.Context;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore.Services
{
    public class TokenObserverService : BackgroundService, IObserver<DataCollectorService.ChainData>
    {
        private const string TASK_VERSION = "0.9-db-persistance";
        private const string TASK_SYNC_EVENT_ADDRESS =
            "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        private const int TASK_TASK_DELAY_MS = 60000;
        private readonly string _baseUri;
        private readonly DataCollectorService.ChainDataHandler _chainDataHandler;
        private readonly string _chainName;

        private readonly Serilog.Core.Logger _log;
        private readonly TelegramNotifier _telegramNotifier;
        private readonly List<string> _tokenAddressToCompareWith;

        private readonly string _tokenFileName;
        private IDisposable _cancellation = null!;

        // Initialize configuration accessor
        private IConfigurationRoot? _configuration;

        private TokenListConfig _tokenList = null!;


        public TokenObserverService(
            string chainName,
            TelegramNotifier telegramNotifier,
            DataCollectorService.ChainDataHandler chainDataHandler,
            List<string> addressesToCompare,
            string tokenFileName, string baseUri)
        {
            _chainName = chainName;
            _telegramNotifier = telegramNotifier;
            _chainDataHandler = chainDataHandler;
            _tokenAddressToCompareWith = addressesToCompare;
            _baseUri = baseUri;
            _tokenFileName = tokenFileName;

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

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public async void OnNext(DataCollectorService.ChainData chainData)
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
                return;
            }

            _log.Information("New Data Received");
            if (chainData.Transactions.Count <= 0) return;
            _tokenList = _configuration.Get<TokenListConfig>();
            _tokenList.blacklisted ??= new List<string>();
            List<string> completeTokenList = _tokenList.whitelisted.Concat(_tokenList.blacklisted).ToList();

            _log.Information(
                $"Analyzing addresses:{Environment.NewLine}{JsonConvert.SerializeObject(_tokenAddressToCompareWith, Formatting.Indented)}");
            // Select only transaction from the address that we need analyze
            var transactionsToAnalyze = chainData.Transactions
                .Where(t =>
                    _tokenAddressToCompareWith.Contains(t.Transaction.From.ToLower()) ||
                    _tokenAddressToCompareWith.Contains(t.Transaction.To.ToLower()));
            var enTransactions = transactionsToAnalyze.ToList();
            _log.Debug($"Total transaction to analyze: {enTransactions.Count().ToString()}");
            foreach (var t in enTransactions)
            {
                var poolsUsed = GetPoolUsedFromTransaction(t);
                foreach (var poolContractHandler in poolsUsed.Select(pool =>
                    chainData.Web3.Eth.GetContractHandler(pool.ToString())))
                {
                    try
                    {
                        List<string> tokens = await GetTokensFromPool(poolContractHandler);
                        if (tokens.Count <= 0) return;
                        var poolFactory = poolContractHandler.QueryAsync<FactoryFunction, string>();
                        poolFactory.Wait();
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
                            var tokenContractHandler = chainData.Web3.Eth.GetContractHandler(token);
                            var tokenSymbol = tokenContractHandler.QueryAsync<SymbolFunction, string>();
                            tokenSymbol.Wait();
                            var tokenTotalSupply =
                                tokenContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>();
                            tokenTotalSupply.Wait();
                            _log.Debug(
                                $"[  ] {tokenSymbol.Result} {tokenTotalSupply.Result.ToString()} {t.Transaction.TransactionHash} {poolFactory.Result}");
                            EvaluateToken(
                                token,
                                tokenSymbol.Result,
                                t.Transaction.TransactionHash,
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
            
            _log.Information("Analysis complete");
            await NotifyMissingTokens();
            _log.Information("Cleaning Tokens");
            // Clean up Telegram
            await CleanUpTelegram(new TokenDbContext(), completeTokenList, _telegramNotifier);
        }

        private async Task CleanUpTelegram(TokenDbContext context,
            ICollection<string> knownToken,
            TelegramNotifier telegramNotifier)
        {
            // Iterate all Notified Tokens
            var notifiedTokens = context.Tokens.Where(o => o.Notified == true);
            foreach (var token in notifiedTokens)
            {
                if (!knownToken.Contains(token.TokenAddress)) continue;
                _log.Information($"Deleting messageId: {token.TelegramMsgId}...");
                await telegramNotifier.DeleteMessageAsync(token.TelegramMsgId);
            }
        }

        private static IEnumerable<JToken> GetPoolUsedFromTransaction(DataCollectorService.ChainData.EnTransaction enT)
        {
            var logsList = enT.TransactionReceipt.Logs;

            var syncEventsInLogs = logsList.Where(
                e => string.Equals(e["topics"][0].ToString().ToLower(),
                    TASK_SYNC_EVENT_ADDRESS, StringComparison.Ordinal)
            ).ToList();
            // Can't find any sync event, next...
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

        private void EvaluateToken(string token, string tokenSymbol, string txHash, string poolAddress,
            string exchangeAddress, DataCollectorService.ChainData.EnTransaction t)
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
                        entity.From = t.Transaction.From;
                        entity.To = t.Transaction.To;
                        db.Entry(entity).Collection(c => c.Exchanges).Load();
                        db.Entry(entity).Collection(c=>c.Pools).Load();
                        if (entity.Exchanges.FirstOrDefault(e => e.Address==exchangeAddress.ToLower()) == null)
                            entity.Exchanges.Add(new DbLayer.Models.Exchange() { Address = exchangeAddress.ToLower() });
                        if (entity.Pools.FirstOrDefault(p => p.Address==poolAddress.ToLower()) == null)
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
                        From = t.Transaction.From,
                        To = t.Transaction.To,
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
                        Hash = t.Transaction.TransactionHash
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
                        $"  totalSupplyChanged: {t.IsDeflationary.ToString()}",
                        $"  totalTxCount: {t.TxCount.ToString()}",
                        $"  lastTxSeen: <a href='{_baseUri}tx/{transactionHash}'>{transactionHash[..10]}...{transactionHash[^10..]}</a>",
                        $"  from: <a href='{_baseUri}{t.From}'>{t.From[..10]}...{t.From[^10..]}</a>",
                        $"  to: <a href='{_baseUri}{t.To}'>{t.To[..10]}...{t.To[^10..]}</a>",
                        $"  pools: [{Environment.NewLine}{string.Join(Environment.NewLine, pools.Select(p => $"    <a href='{_baseUri}address/{p.Address.ToString()}'>{p.Address.ToString()[..10]}...{p.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  exchanges: [{Environment.NewLine}{string.Join(Environment.NewLine, t.Exchanges.Select(e => $"    <a href='{_baseUri}address/{e.Address.ToString()}'>{e.Address.ToString()[..10]}...{e.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        "  ver: 0.9-db-persistence"
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
            _telegramNotifier.SendMessage($"Starting TokenObserverService for chain: {_chainName} with version: {TASK_VERSION}");
            stoppingToken.Register(() =>
                {
                    Unsubscribe();
                    _log.Information($"TokenObserverService background task is stopping for chain: {_chainName}");
                }
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                Subscribe(_chainDataHandler);
                await Task.Delay(TASK_TASK_DELAY_MS, stoppingToken);
            }
        }

        private void Subscribe(DataCollectorService.ChainDataHandler provider)
        {
            _cancellation = provider.Subscribe(this);
        }

        private void Unsubscribe()
        {
            _cancellation.Dispose();
        }
    }
}
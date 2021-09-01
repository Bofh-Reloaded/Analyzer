#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using Serilog;
using Microsoft.Extensions.Configuration;
using Nethereum.Contracts.ContractHandlers;
using Newtonsoft.Json;
using Serilog.Context;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore.Services
{
    public class TokenObserverService : BackgroundService, IObserver<DataCollectorService.ChainData>
    {
        private const string SyncEventAddress = "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        private const int TaskDelayMs = 60000;
        private readonly string _baseUri;
        private readonly DataCollectorService.ChainDataHandler _chainDataHandler;
        private readonly string _chainName;

        // Initialize configuration accessor
        private IConfigurationRoot? _configuration;

        private readonly Serilog.Core.Logger _log;

        private ConcurrentDictionary<string, Token> _missingTokens = null!;
        private readonly TelegramNotifier _telegramNotifier;
        private readonly List<string> _tokenAddressToCompareWith;
        private IDisposable _cancellation = null!;

        private readonly string _tokenFileName;
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
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
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

        public void OnNext(DataCollectorService.ChainData chainData)
        {
            _telegramNotifier.SendMessage("Reloading Tokens...");
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
                return;
            }

            _missingTokens = new ConcurrentDictionary<string, Token>();

            _log.Information("New Data Received");
            if (chainData.Transactions.Count <= 0) return;
            _tokenList = _configuration.Get<TokenListConfig>();
            _tokenList.blacklisted ??= new List<string>();

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
                var logsList = t.TransactionReceipt.Logs;
                var syncEvents = logsList.Where(
                    e => string.Equals(e["topics"][0].ToString().ToLower(),
                        SyncEventAddress, StringComparison.Ordinal)
                ).ToList();
                Parallel.ForEach(
                    syncEvents.Select(
                            contract => contract["address"].ToString()
                        )
                        .Select(contractAddress => chainData.Web3.Eth.GetContractHandler(contractAddress)),
                    contractHandler =>
                    {
                        try
                        {
                            var tokens = AnalyzeSyncEvent(contractHandler);
                            tokens.Wait();
                            if (tokens.Result.Count <= 0) return;
                            var poolFactory = contractHandler.QueryAsync<FactoryFunction, string>();
                            poolFactory.Wait();
                            foreach (var token in tokens.Result)
                            {
                                _log.Debug($"[ ] Token: {token}");
                                var tokenContractHandler = chainData.Web3.Eth.GetContractHandler(token);
                                var tokenSymbol = tokenContractHandler.QueryAsync<SymbolFunction, string>();
                                tokenSymbol.Wait();
                                var tokenTotalSupply =
                                    tokenContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>();
                                tokenTotalSupply.Wait();
                                _log.Debug(
                                    $"[  ] {tokenSymbol.Result} {tokenTotalSupply.Result.ToString()} {t.Transaction.TransactionHash} {poolFactory}");
                                EvaluateToken(
                                    token,
                                    tokenSymbol.Result,
                                    tokenTotalSupply.Result,
                                    t.Transaction.TransactionHash,
                                    contractHandler.ContractAddress,
                                    poolFactory.Result,
                                    t);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            _log.Error("Error during request, skipping token...");
                        }
                    });
            }

            _log.Information("Analysis complete");
            NotifyMissingTokens();
        }

        private async Task<List<string>> AnalyzeSyncEvent(ContractHandler contractHandler)
        {
            try
            {
                var tokens = new List<string>();
                _log.Debug($"Called AnalyzeSyncEvent on {contractHandler.ContractAddress}");
                var token0OutputDto =
                    await contractHandler.QueryDeserializingToObjectAsync<Token0Function, Token0OutputDTO>();
                if ((_tokenList.whitelisted.Contains(token0OutputDto.ReturnValue1) ||
                     _tokenList.blacklisted.Contains(token0OutputDto.ReturnValue1)))
                {
                    _log.Debug($"Token: {token0OutputDto.ReturnValue1} already known");
                }
                else
                {
                    tokens.Add(token0OutputDto.ReturnValue1);
                }

                var token1OutputDto =
                    await contractHandler.QueryDeserializingToObjectAsync<Token1Function, Token1OutputDTO>();
                if ((_tokenList.whitelisted.Contains(token1OutputDto.ReturnValue1) ||
                     _tokenList.blacklisted.Contains(token1OutputDto.ReturnValue1)))
                {
                    _log.Debug($"Token: {token1OutputDto.ReturnValue1} already known");
                }
                else
                {
                    tokens.Add(token1OutputDto.ReturnValue1);
                }

                return tokens;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void EvaluateToken(string token, string tokenSymbol, BigInteger tokenTotalSupply, string txHash,
            string poolAddress,
            string exchangeAddress, DataCollectorService.ChainData.EnTransaction t)
        {
            try
            {
                // Check if the token is already in the list
                if (_missingTokens.ContainsKey(token))
                {
                    // Token exists, check if the supply is changed
                    if (tokenTotalSupply < _missingTokens[token].TokenTotalSupply)
                    {
                        _log.Information(
                            $"Token: {token} changed supply from {_missingTokens[token].TokenTotalSupply.ToString()} to {tokenTotalSupply.ToString()}");
                        _missingTokens[token].IsDeflationary = true;
                    }

                    // Update token details if we haven't seen that trx yet
                    if (!_missingTokens[token].TransactionHashes.Contains(txHash))
                    {
                        _missingTokens[token].TransactionHashes.Add(txHash);
                        _missingTokens[token].TokenTotalSupply = tokenTotalSupply;
                        _missingTokens[token].TxCount++;
                        _missingTokens[token].From = t.Transaction.From;
                        _missingTokens[token].To = t.Transaction.To;
                        if (!_missingTokens[token]
                            .ExchangesList.Contains(exchangeAddress.ToLower()))
                            _missingTokens[token]
                                .ExchangesList.Add(exchangeAddress.ToLower());
                        if (!_missingTokens[token]
                            .PoolsList.Contains(poolAddress.ToLower()))
                            _missingTokens[token]
                                .PoolsList.Add(poolAddress.ToLower());
                    }
                }
                else
                {
                    // Create the object inside the dictionary since it's the first time that we see it
                    _missingTokens[token] = new Token
                    {
                        ExchangesList = new List<string>(),
                        TokenAddress = token,
                        TransactionHashes = new List<string>(),
                        TokenSymbol = tokenSymbol,
                        TokenTotalSupply = tokenTotalSupply,
                        IsDeflationary = false,
                        TxCount = 1,
                        From = t.Transaction.From,
                        To = t.Transaction.To,
                        PoolsList = new List<string>()
                    };
                    _missingTokens[token].ExchangesList.Add(exchangeAddress.ToLower());
                    _missingTokens[token].PoolsList.Add(poolAddress.ToLower());
                }

                _log.Information(
                    $"Found missing Token: {token} with TxHash: {txHash} with {_missingTokens[token].ExchangesList.Count} pools, total txCount: {_missingTokens[token].TxCount.ToString()}");
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
                throw;
            }
        }

        private void NotifyMissingTokens()
        {
            _log.Debug($"MissingTokens: {_missingTokens.Count.ToString()}");
            if (_missingTokens.Count <= 0)
            {
                _log.Information("No Missing token found this time");
                _telegramNotifier.SendMessage("No Missing Tokens inside the analyzed transactions");
                return;
            }

            foreach (var tFound in _missingTokens.Values
                .ToList()
                .OrderBy(o => o.TxCount)
                .TakeLast(10)
                .Select(
                    t =>
                        string.Join(
                            Environment.NewLine,
                            $"<b>{t.TokenSymbol} [<a href='https://bscscan.com/token/{t.TokenAddress}'>{t.TokenAddress}</a>]:</b>",
                            $"  totalSupplyChanged: {t.IsDeflationary.ToString()}",
                            $"  totalTxCount: {t.TxCount.ToString()}",
                            $"  lastTxSeen: <a href='{_baseUri}tx/{t.GetLatestTxHash()}'>{t.GetLatestTxHash()[..10]}...{t.GetLatestTxHash()[^10..]}</a>",
                            $"  from: <a href='{_baseUri}{t.From}'>{t.From[..10]}...{t.From[^10..]}</a>",
                            $"  to: <a href='{_baseUri}{t.To}'>{t.To[..10]}...{t.To[^10..]}</a>",
                            $"  pools: [{Environment.NewLine}{string.Join(Environment.NewLine, t.PoolsList.Select(p => $"    <a href='{_baseUri}address/{p.ToString()}'>{p.ToString()[..10]}...{p.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                            $"  exchanges: [{Environment.NewLine}{string.Join(Environment.NewLine, t.ExchangesList.Select(e => $"    <a href='{_baseUri}address/{e.ToString()}'>{e.ToString()[..10]}...{e.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]"
                        )))
            {
                _telegramNotifier.SendMessage(tFound);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information($"Starting TokenObserverService for chain: {_chainName}");
            _telegramNotifier.SendMessage($"Starting TokenObserverService for chain: {_chainName}");
            stoppingToken.Register(() =>
                {
                    Unsubscribe();
                    _log.Information($"TokenObserverService background task is stopping for chain: {_chainName}");
                }
            );

            while (!stoppingToken.IsCancellationRequested)
            {
                Subscribe(_chainDataHandler);
                await Task.Delay(TaskDelayMs, stoppingToken);
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
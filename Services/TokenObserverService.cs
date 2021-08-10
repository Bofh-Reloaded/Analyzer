#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using AnalyzerCore.Notifier;
using log4net;
using Microsoft.Extensions.Configuration;

namespace AnalyzerCore.Services
{
    public class TokenObserverService : BackgroundService, IObserver<DataCollectorService.ChainData>
    {
        private const string SyncEventAddress = "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        private const int TaskDelayMs = 60000;
        private readonly DataCollectorService.ChainDataHandler _chainDataHandler;
        private readonly string _chainName;

        // Initialize configuration accessor
        private readonly IConfigurationRoot? _configuration;

        // Initialize Logger
        private readonly ILog _log;

        private readonly ConcurrentDictionary<string, Token> _missingTokens;
        private readonly TelegramNotifier _telegramNotifier;
        private readonly List<string> _tokenAddressToCompareWith;
        private IDisposable? _cancellation;

        private TokenListConfig _tokenList;

        public TokenObserverService(
            string chainName,
            TelegramNotifier telegramNotifier,
            DataCollectorService.ChainDataHandler chainDataHandler,
            List<string> addressesToCompare,
            string tokenFileName
        )
        {
            _chainName = chainName;
            _telegramNotifier = telegramNotifier;
            _chainDataHandler = chainDataHandler;
            _tokenAddressToCompareWith = addressesToCompare;
            _log = LogManager.GetLogger($"{MethodBase.GetCurrentMethod()?.DeclaringType}: {this._chainName}");
            // Load configuration regarding tokens
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile(tokenFileName, false, true)
                .Build();
            _tokenList = _configuration.Get<TokenListConfig>();
            _missingTokens = new ConcurrentDictionary<string, Token>();
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
            _log.Info("New Data Received");
            if (chainData.Transactions.Count <= 0) return;
            _tokenList = _configuration.Get<TokenListConfig>();
            _log.Info($"Analyzing addresses: {string.Join(Environment.NewLine, _tokenAddressToCompareWith.ToArray())}");
            // Select only transaction from the address that we need analyze
            var transactionsToAnalyze = chainData.Transactions
                .Where(t =>
                    _tokenAddressToCompareWith.Contains(t.Transaction.From) ||
                    _tokenAddressToCompareWith.Contains(t.Transaction.To));
            var enTransactions = transactionsToAnalyze.ToList();
            _log.Debug($"Total transaction to analyze: {enTransactions.Count().ToString()}");
            Parallel.ForEach(enTransactions, t =>
            {
                var logsList = t.TransactionReceipt.Logs;
                var syncEvents = logsList.Where(
                    e => string.Equals(e["topics"][0].ToString().ToLower(),
                        SyncEventAddress, StringComparison.Ordinal)
                ).ToList();
                foreach (var contractHandler in syncEvents.Select(contract => contract["address"]
                        .ToString())
                    .Select(contractAddress => chainData.Web3.Eth.GetContractHandler(contractAddress)))
                {
                    var token0OutputDto =
                        contractHandler.QueryDeserializingToObjectAsync<Token0Function, Token0OutputDTO>();
                    token0OutputDto.Wait();
                    var poolFactory = contractHandler.QueryAsync<FactoryFunction, string>();
                    poolFactory.Wait();
                    var token1OutputDto =
                        contractHandler.QueryDeserializingToObjectAsync<Token1Function, Token1OutputDTO>();
                    token1OutputDto.Wait();
                    foreach (var token in new List<string>
                        {token0OutputDto.Result.ReturnValue1, token1OutputDto.Result.ReturnValue1})
                    {
                        _log.Debug($"[ ] Token: {token}");
                        var tokenContractHandler = chainData.Web3.Eth.GetContractHandler(token);
                        var tokenSymbol = tokenContractHandler.QueryAsync<SymbolFunction, string>();
                        tokenSymbol.Wait();
                        var tokenTotalSupply = tokenContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>();
                        tokenTotalSupply.Wait();
                        _log.Debug(
                            $"[  ] {tokenSymbol.Result} {tokenTotalSupply.Result.ToString()} {t.Transaction.TransactionHash} {poolFactory.Result}");
                        EvaluateToken(
                            token,
                            tokenSymbol.Result,
                            tokenTotalSupply.Result,
                            t.Transaction.TransactionHash,
                            poolFactory.Result,
                            t);
                    }
                }
            });
            _log.Info("Analysis complete");
            NotifyMissingTokens();
        }

        private void NotifyMissingTokens()
        {
            _log.Debug($"MissingTokens: {_missingTokens.Count.ToString()}");
            if (_missingTokens.Count <= 0)
            {
                _log.Info("No Missing token found this time");
                return;
            }

            const string baseUri = "https://bscscan.com/address/";
            foreach (var tFound in _missingTokens.Values
                .ToList()
                .OrderBy(o => o.TxCount)
                .TakeLast(10)
                .Select(
                    t =>
                        string.Join(
                            Environment.NewLine,
                            $"<b>{t.TokenSymbol} [{t.TokenAddress}]:</b>",
                            $"  isDeflationary: {t.IsDeflationary.ToString()}",
                            $"  totalTxCount: {t.TxCount.ToString()}",
                            $"  lastTxSeen: {t.GetTransactionHash()}",
                            $"  from: <a href='{baseUri}{t.From}'>{t.From[..6]}...{t.From[^6..]}</a>",
                            $"  to: <a href='{baseUri}{t.To}'>{t.To[..6]}...{t.To[^6..]}</a>"
                        )))
            {
                var t = Task.Run(async delegate
                {
                    _telegramNotifier.SendMessage(tFound);
                    await Task.Delay(2000);
                });
            }
        }

        private void EvaluateToken(string token, string tokenSymbol, BigInteger tokenTotalSupply, string txHash,
            string factory, DataCollectorService.ChainData.EnTransaction t)
        {
            if (_tokenList.whitelisted.Contains(token) || _tokenList.blacklisted.Contains(token))
            {
                _log.Debug($"[   ] Token: {tokenSymbol} already known");
                return;
            }

            // Check if the token is already in the list
            if (_missingTokens.ContainsKey(token))
            {
                // Token exists, check if the supply is changed
                if (tokenTotalSupply < _missingTokens[token].TokenTotalSupply)
                {
                    _log.Info(
                        $"Token: {token} changed supply from {_missingTokens[token].TokenTotalSupply.ToString()} to {tokenTotalSupply.ToString()}");
                    _missingTokens[token].IsDeflationary = true;
                }

                // Update token details
                if (!_missingTokens[token].TransactionHashes.Contains(txHash))
                {
                    _missingTokens[token].TransactionHashes.Add(txHash);
                    _missingTokens[token].TokenTotalSupply = tokenTotalSupply;
                    _missingTokens[token].TxCount++;
                    _missingTokens[token].From = t.Transaction.From;
                    _missingTokens[token].To = t.Transaction.To;
                }
            }
            else
            {
                // Create the object inside the dictionary since it's the first time that we see it
                _missingTokens[token] = new Token
                {
                    PoolFactory = factory,
                    TokenAddress = token,
                    TransactionHashes = new List<string>(),
                    TokenSymbol = tokenSymbol,
                    TokenTotalSupply = tokenTotalSupply,
                    IsDeflationary = false,
                    TxCount = 1,
                    From = t.Transaction.From,
                    To = t.Transaction.To,
                };
            }

            _log.Info(
                $"Found missing Token: {token} with TxHash: {txHash} within Pool: {factory}, total txCount: {_missingTokens[token].TxCount.ToString()}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Info($"Starting TokenObserverService for chain: {_chainName}");
            _telegramNotifier.SendMessage($"Starting TokenObserverService for chain: {_chainName}");
            stoppingToken.Register(() =>
                {
                    Unsubscribe();
                    _log.Info($"TokenObserverService background task is stopping for chain: {_chainName}");
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
            _cancellation?.Dispose();
        }
    }
}
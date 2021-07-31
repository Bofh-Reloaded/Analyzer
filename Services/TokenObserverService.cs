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
        private const string TokenAddressToCompareWith = "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178";
        private const string SyncEventAddress = "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        private const int TaskDelayMs = 60000;
        private readonly DataCollectorService.ChainDataHandler _chainDataHandler;
        private readonly string _chainName;

        // Initialize Logger
        private readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );

        private readonly ConcurrentDictionary<string, Token> _missingTokens;
        private readonly TelegramNotifier _telegramNotifier;

        private TokenListConfig _tokenList;
        private IDisposable? _cancellation;

        // Initialize configuration accessor
        private readonly IConfigurationRoot? _configuration;

        public TokenObserverService(string chainName, TelegramNotifier telegramNotifier,
            DataCollectorService.ChainDataHandler chainDataHandler)
        {
            _chainName = chainName;
            _telegramNotifier = telegramNotifier;
            _chainDataHandler = chainDataHandler;
            // Load configuration regarding tokens
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("tokens.json", false, true)
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

            // Select only transaction from the address that we need analyze
            var transactionsToAnalyze = chainData.Transactions
                .Where(t => 
                    t.Transaction.From == TokenAddressToCompareWith || t.Transaction.To == TokenAddressToCompareWith);
            Parallel.ForEach(transactionsToAnalyze, t =>
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
                    var token0OutputDto = contractHandler.QueryDeserializingToObjectAsync<Token0Function, Token0OutputDTO>();
                    token0OutputDto.Wait();
                    var poolFactory = contractHandler.QueryAsync<FactoryFunction, string>();
                    poolFactory.Wait();
                    var token1OutputDto = contractHandler.QueryDeserializingToObjectAsync<Token1Function, Token1OutputDTO>();
                    token1OutputDto.Wait();
                    foreach (var token in new List<string> {token0OutputDto.Result.ReturnValue1, token1OutputDto.Result.ReturnValue1})
                    {
                        var tokenContractHandler = chainData.Web3.Eth.GetContractHandler(token);
                        var tokenSymbol = tokenContractHandler.QueryAsync<SymbolFunction, string>();
                        tokenSymbol.Wait();
                        var tokenTotalSupply = tokenContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>();
                        tokenTotalSupply.Wait();
                        EvaluateToken(token, tokenSymbol.Result, tokenTotalSupply.Result, t.Transaction.TransactionHash, poolFactory.Result);
                    }
                }
            });
            _log.Info("Analysis complete");
            NotifyMissingTokens();
        }
        private void NotifyMissingTokens()
        {
            if (_missingTokens.Count <= 0) return;
            _telegramNotifier.SendMessage(
                string.Join(
                    Environment.NewLine,
                    _missingTokens.Values
                        .ToList()
                        .OrderBy(o=>o.TxCount)
                        .TakeLast(10)
                        .Select(
                            t => $"<b>{t.TokenSymbol}:</b>{Environment.NewLine}  token: {t.TokenAddress}{Environment.NewLine}  isDeflationary: {t.IsDeflationary.ToString()}{Environment.NewLine}  totalTxCount: {t.TxCount.ToString()}{Environment.NewLine}  lastTxSeen: {t.GetTransactionHash()}"
                            )
                ));
        }

        private void EvaluateToken(string token, string tokenSymbol, BigInteger tokenTotalSupply, string txHash, string factory)
        {
            if (_tokenList.whitelisted.Contains(token) || _tokenList.blacklisted.Contains(token)) return;
            // Check if the token is already in the list
            if (_missingTokens.ContainsKey(token))
            {
                // Token exists, check if the supply is changed
                if (tokenTotalSupply < _missingTokens[token].TokenTotalSupply)
                {
                    _log.Info($"Token: {token} changed supply from {_missingTokens[token].TokenTotalSupply.ToString()} to {tokenTotalSupply.ToString()}");
                    _missingTokens[token].IsDeflationary = true;
                }
                // Update token details, maybe this is not needed but anyway...
                _missingTokens[token].TransactionHash = txHash;
                _missingTokens[token].TokenTotalSupply = tokenTotalSupply;
                _missingTokens[token].TxCount++;
            }
            else
            {
                // Create the object inside the dictionary since it's the first time that we see it
                _missingTokens[token] = new Token
                {
                    PoolFactory = factory,
                    TokenAddress = token,
                    TransactionHash = txHash,
                    TokenSymbol = tokenSymbol,
                    TokenTotalSupply = tokenTotalSupply,
                    IsDeflationary = false,
                    TxCount = 1
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
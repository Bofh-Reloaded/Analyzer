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
using Org.BouncyCastle.Crypto.Engines;

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

        private readonly BlockingCollection<Token> _missingTokens;
        private readonly TelegramNotifier _telegramNotifier;

        private readonly TokenListConfig _tokenList;
        private IDisposable? _cancellation;

        // Initialize configuration accessor
        public IConfigurationRoot? Configuration;

        public TokenObserverService(string chainName, TelegramNotifier telegramNotifier,
            DataCollectorService.ChainDataHandler chainDataHandler)
        {
            _chainName = chainName;
            _telegramNotifier = telegramNotifier;
            _chainDataHandler = chainDataHandler;
            // Load configuration regarding tokens
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("tokens.json", false, true)
                .Build();
            _tokenList = configuration.Get<TokenListConfig>();
            _missingTokens = new BlockingCollection<Token>();
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

            // Select only transaction from the address that we need analyze
            var transactionsToAnalyze = chainData.Transactions
                .Where(t =>
                    t.Transaction.From == TokenAddressToCompareWith || t.Transaction.To == TokenAddressToCompareWith);
            Parallel.ForEach(transactionsToAnalyze, async t =>
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
                        await contractHandler.QueryDeserializingToObjectAsync<Token0Function, Token0OutputDTO>();
                    var poolFactory = await contractHandler.QueryAsync<FactoryFunction, string>();
                    var token1OutputDto =
                        await contractHandler.QueryDeserializingToObjectAsync<Token1Function, Token1OutputDTO>();
                    foreach (var token in new List<string> {token0OutputDto.ReturnValue1, token1OutputDto.ReturnValue1})
                    {
                        var tokenContractHandler = chainData.Web3.Eth.GetContractHandler(token);
                        var tokenSymbol = await tokenContractHandler.QueryAsync<SymbolFunction, string>();
                        var tokenTotalSupply = await tokenContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>();
                        EvaluateToken(token, tokenSymbol, tokenTotalSupply, t.Transaction.TransactionHash, poolFactory);
                    }
                }
            });
            _log.Info("Analysis complete");
        }

        private void NotifyMissingTokens()
        {
            if (_missingTokens.Count <= 0) return;
            _telegramNotifier.SendMessage(
                string.Join(
                    Environment.NewLine,
                    _missingTokens.Select(t => $"{t.TokenSymbol}: {t.TokenAddress}, totalSupply: {t.TokenTotalSupply.ToString()}, isDeflationary: {t.IsDeflationary.ToString()}")
                ));
            while (_missingTokens.TryTake(out _)){}
        }

        private void EvaluateToken(string token, string tokenSymbol, BigInteger tokenTotalSupply, string txHash, string factory)
        {
            if (_tokenList.whitelisted.Contains(token) || _tokenList.blacklisted.Contains(token)) return;
            if ((_missingTokens.Where(m => m.TokenAddress == token)).Any()) return;
            _log.Info(
                $"Found missing Token: {token} with TxHash: {txHash} within Exchange: {factory}");
            var isDeflationary = _missingTokens.Where(t => t.TokenAddress == token)
                                     .Select(t => t.TokenTotalSupply)
                                     .ToString() !=
                                 tokenTotalSupply.ToString();
            _missingTokens.Add(new Token()
            {
                PoolFactory = factory,
                TokenAddress = token,
                TransactionHash = txHash,
                TokenSymbol = tokenSymbol,
                TokenTotalSupply = tokenTotalSupply.ToString(),
                IsDeflationary = isDeflationary
            });
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
                NotifyMissingTokens();
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
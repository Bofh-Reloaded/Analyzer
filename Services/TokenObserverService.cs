using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using AnalyzerCore.Models.GethModels;
using log4net;
using Microsoft.Extensions.Configuration;
using Nethereum.Web3;

namespace AnalyzerCore.Services
{
    public class TokenObserverService : BackgroundService
    {
        private const string TokenAddressToCompareWith = "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178";
        private const string SyncEventAddress = "0x1c411e9a96e071241c2f21f7726b17ae89e3cab4c78be50e062b03a9fffbbad1";

        private readonly TokenListConfig _tokenList;
        
        private readonly List<string> _missingTokens;
        
        // Initialize Logger
        private readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );
        
        private readonly Web3 _web3;
        
        public TokenObserverService(string uri)
        {
            // Load configuration regarding tokens
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("tokens.json", false, true)
                .Build();
            _tokenList = configuration.Get<TokenListConfig>();
            _missingTokens = new List<string>();
            _web3 = new Web3(uri);
        }
        
        private async Task AnalyzeMissingTokens(List<EnhancedTransaction> transactions)
        {
            /*
            // Analyze Tokens
            if (!string.Equals(currentAddress, TokenAddressToCompareWith,
                StringComparison.CurrentCultureIgnoreCase)) return;
            {
                var logsList = receipt.Result.Logs.ToList();
                var syncEvents = logsList.Where(
                    e => string.Equals(e["topics"][0].ToString().ToLower(),
                        SyncEventAddress, StringComparison.Ordinal)
                ).ToList();
                foreach (var contract in syncEvents)
                {
                    var contractAddress = contract["address"].ToString();
                    var contractHandler = _web3.Eth.GetContractHandler(contractAddress);
                    var token0 =
                        await contractHandler.QueryDeserializingToObjectAsync<Token0Function, Token0OutputDTO>();
                    var token1 =
                        await contractHandler.QueryDeserializingToObjectAsync<Token1Function, Token1OutputDTO>();

                    if (!_tokenList.whitelisted.Contains(token0.ReturnValue1) &&
                        !_tokenList.blacklisted.Contains(token1.ReturnValue1))
                    {
                        if (!_missingTokens.Contains(token0.ReturnValue1)) _missingTokens.Add(token0.ReturnValue1);
                        _log.Info($"Found missing token: {token0.ReturnValue1}");
                    }

                    if (!_tokenList.whitelisted.Contains(token1.ReturnValue1) &&
                        !_tokenList.blacklisted.Contains(token1.ReturnValue1))
                    {
                        if (!_missingTokens.Contains(token1.ReturnValue1)) _missingTokens.Add(token1.ReturnValue1);
                        _log.Info($"Found missing token: {token1.ReturnValue1}");
                    }
                }
            }
            */
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            /*
            while (!stoppingToken.IsCancellationRequested)
            {
                await AnalyzeMissingTokens(currentAddress, receipt);
                if (_missingTokens.Count > 0)
                    _telegramNotifier.SendMessage(
                        $"Missing Tokens: {Environment.NewLine} {string.Join(Environment.NewLine, _missingTokens.ToArray())}");
                _missingTokens.Clear();
            }
            */
        }
    }
}
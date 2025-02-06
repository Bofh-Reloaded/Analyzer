using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Models;
using AnalyzerCore.Domain.Services;
using AnalyzerCore.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace AnalyzerCore.Infrastructure.Blockchain
{
    public class BlockchainService : IBlockchainService
    {
        private readonly Web3 _web3;
        private readonly ILogger<BlockchainService> _logger;
        private readonly ChainConfig _chainConfig;

        public BlockchainService(
            Web3 web3,
            ChainConfig chainConfig,
            ILogger<BlockchainService> logger)
        {
            _web3 = web3;
            _chainConfig = chainConfig;
            _logger = logger;
        }

        public async Task<BigInteger> GetCurrentBlockNumberAsync(CancellationToken cancellationToken = default)
        {
            var blockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            return blockNumber.Value;
        }

        public async Task<IEnumerable<BlockData>> GetBlocksAsync(
            BigInteger fromBlock,
            BigInteger toBlock,
            CancellationToken cancellationToken = default)
        {
            var blocks = new List<BlockData>();
            var batchSize = 10;
            
            for (var i = fromBlock; i <= toBlock; i += batchSize)
            {
                var tasks = new List<Task<BlockWithTransactions>>();
                var end = BigInteger.Min(i + batchSize - 1, toBlock);
                
                for (var blockNumber = i; blockNumber <= end; blockNumber++)
                {
                    var task = _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                        .SendRequestAsync(new HexBigInteger(blockNumber));
                    tasks.Add(task);
                }

                var results = await Task.WhenAll(tasks);
                
                foreach (var block in results.Where(b => b != null))
                {
                    blocks.Add(new BlockData
                    {
                        Number = block.Number.Value,
                        Hash = block.BlockHash,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).UtcDateTime,
                        Transactions = block.Transactions.Select(tx => new TransactionInfo
                        {
                            Hash = tx.TransactionHash,
                            From = tx.From,
                            To = tx.To,
                            Value = tx.Value.Value,
                            Input = tx.Input,
                            GasUsed = tx.Gas.Value,
                            Status = true,
                            Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).UtcDateTime
                        })
                    });
                }
            }

            return blocks;
        }

        public async Task<TokenInfo> GetTokenInfoAsync(string address, CancellationToken cancellationToken = default)
        {
            var contract = _web3.Eth.GetContract(ERC20ABI.ABI, address);
            
            var nameTask = contract.GetFunction("name").CallAsync<string>();
            var symbolTask = contract.GetFunction("symbol").CallAsync<string>();
            var decimalsTask = contract.GetFunction("decimals").CallAsync<int>();
            var totalSupplyTask = contract.GetFunction("totalSupply").CallAsync<BigInteger>();

            await Task.WhenAll(nameTask, symbolTask, decimalsTask, totalSupplyTask);

            return new TokenInfo
            {
                Address = address,
                Name = await nameTask,
                Symbol = await symbolTask,
                Decimals = await decimalsTask,
                TotalSupply = Web3.Convert.FromWei(await totalSupplyTask)
            };
        }

        public async Task<PoolInfo> GetPoolInfoAsync(string address, CancellationToken cancellationToken = default)
        {
            var contract = _web3.Eth.GetContract(UniswapV2PairABI.ABI, address);
            
            var token0Task = contract.GetFunction("token0").CallAsync<string>();
            var token1Task = contract.GetFunction("token1").CallAsync<string>();
            var factoryTask = contract.GetFunction("factory").CallAsync<string>();
            var reservesTask = contract.GetFunction("getReserves").CallAsync<Reserves>();

            await Task.WhenAll(token0Task, token1Task, factoryTask, reservesTask);
            var reserves = await reservesTask;

            return new PoolInfo
            {
                Address = address,
                Token0 = await token0Task,
                Token1 = await token1Task,
                Factory = await factoryTask,
                Reserve0 = Web3.Convert.FromWei(reserves.Reserve0),
                Reserve1 = Web3.Convert.FromWei(reserves.Reserve1),
                Type = PoolType.UniswapV2
            };
        }

        public async Task<(decimal Reserve0, decimal Reserve1)> GetPoolReservesAsync(
            string address,
            CancellationToken cancellationToken = default)
        {
            var contract = _web3.Eth.GetContract(UniswapV2PairABI.ABI, address);
            var reserves = await contract.GetFunction("getReserves").CallAsync<Reserves>();
            
            return (
                Web3.Convert.FromWei(reserves.Reserve0),
                Web3.Convert.FromWei(reserves.Reserve1)
            );
        }

        public async Task<bool> IsContractAsync(string address, CancellationToken cancellationToken = default)
        {
            var code = await _web3.Eth.GetCode.SendRequestAsync(address);
            return code != "0x";
        }

        public async Task<string> GetContractCreatorAsync(string address, CancellationToken cancellationToken = default)
        {
            var transaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(address);
            return transaction?.From;
        }

        public async Task<IEnumerable<TransactionInfo>> GetTransactionsByAddressAsync(
            string address,
            BigInteger fromBlock,
            BigInteger toBlock,
            CancellationToken cancellationToken = default)
        {
            var filterInput = new NewFilterInput
            {
                FromBlock = new BlockParameter(new HexBigInteger(fromBlock)),
                ToBlock = new BlockParameter(new HexBigInteger(toBlock)),
                Address = new[] { address }
            };

            var logs = await _web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);
            var transactions = new List<TransactionInfo>();

            foreach (var log in logs)
            {
                var transaction = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(log.TransactionHash);
                if (transaction != null)
                {
                    var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                        .SendRequestAsync(log.BlockNumber);

                    transactions.Add(new TransactionInfo
                    {
                        Hash = transaction.TransactionHash,
                        From = transaction.From,
                        To = transaction.To,
                        Value = transaction.Value.Value,
                        Input = transaction.Input,
                        GasUsed = transaction.Gas.Value,
                        Status = true,
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value).UtcDateTime
                    });
                }
            }

            return transactions;
        }

        public async Task<decimal> GetTokenBalanceAsync(
            string tokenAddress,
            string walletAddress,
            CancellationToken cancellationToken = default)
        {
            var contract = _web3.Eth.GetContract(ERC20ABI.ABI, tokenAddress);
            var balance = await contract.GetFunction("balanceOf")
                .CallAsync<BigInteger>(walletAddress);
            
            return Web3.Convert.FromWei(balance);
        }

        [FunctionOutput]
        private class Reserves
        {
            [Parameter("uint112", "_reserve0", 1)]
            public BigInteger Reserve0 { get; set; }

            [Parameter("uint112", "_reserve1", 2)]
            public BigInteger Reserve1 { get; set; }

            [Parameter("uint32", "_blockTimestampLast", 3)]
            public uint BlockTimestampLast { get; set; }
        }
    }
}
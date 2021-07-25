using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace AnalyzerCore.Services
{
    public partial class DataCollectorService
    {
        public class ChainData
        {
            private readonly string _chainName;
            private readonly Task<HexBigInteger> _currentBlock;
            private readonly ILog _log;
            private readonly int _maxParallelism;
            private readonly Web3 _web3;
            public readonly BlockingCollection<EnTransaction> Transactions;
            public readonly Dictionary<string, Address> Addresses;
            private readonly List<string> _addrs;


            public ChainData(Web3 web3, string chainName, int maxParallelism, ILog log, List<string> addresses)
            {
                _web3 = web3;
                _chainName = chainName;
                _maxParallelism = maxParallelism;
                _log = log;
                Transactions = new BlockingCollection<EnTransaction>();
                Addresses = new Dictionary<string, Address>();
                _addrs = addresses;
                // Reading current last block processed on chain
                _currentBlock = _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                _currentBlock.Wait();
            }

            public HexBigInteger CurrentBlock => _currentBlock.Result;

            public void GetBlocks(long startBlock, long currentBlock, CancellationToken cancellationToken)
            {
                var totaltrx = 0;
                var blockNum = 1;
                Parallel.For(startBlock, currentBlock,
                    new ParallelOptions {MaxDegreeOfParallelism = _maxParallelism}, async b =>
                    {
                        // Retrieve Transactions inside block X
                        var blockParameter = new BlockParameter((ulong) b);
                        var block = _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                            .SendRequestAsync(blockParameter);
                        block.Wait(cancellationToken);
                        Log.Debug(
                            $"[{blockNum.ToString()}/500] block: {b.ToString()}, total trx: {block.Result.Transactions.Length.ToString()}");
                        blockNum++;
                        var txCounter = 0;
                        Parallel.ForEach(block.Result.Transactions, e =>
                        {
                            // Skip if we don't care about the address
                            if (!_addrs.Contains(e.From) && !_addrs.Contains(e.To)) return;
                            totaltrx++;
                            var txReceipt = _web3.Eth.Transactions.GetTransactionReceipt
                                .SendRequestAsync(e.TransactionHash);
                            txReceipt.Wait(cancellationToken);
                            var enTx = new EnTransaction
                            {
                                Transaction = e,
                                TransactionReceipt = txReceipt.Result
                            };
                            // Filling the blocking collection
                            Transactions.Add(enTx, cancellationToken);
                            txCounter++;
                            _log.Debug(
                                $" {b.ToString()} -> txCounter: {txCounter.ToString()}, txHash: {enTx.Transaction.TransactionHash}, receiptStatus: {enTx.TransactionReceipt.Status}");
                        });
                    });

                _log.Debug(
                    $"Total trx: {totaltrx.ToString()} chainData.Transactions: {this.Transactions.Count.ToString()}");
            }

            public void GetAddressTransactions(string address)
            {
                Addresses[address] = new Address
                {
                    Transactions = Transactions.Where(t =>
                            TransactionExtensions.IsFrom(t.Transaction, address) ||
                            TransactionExtensions.IsTo(t.Transaction, address))
                        .ToList()
                };
            }

            public class Address
            {
                public List<EnTransaction> Transactions { get; set; }
            }

            public class EnTransaction
            {
                public TransactionReceipt TransactionReceipt { get; set; }
                public Transaction Transaction { get; set; }
            }
        }
    }
}
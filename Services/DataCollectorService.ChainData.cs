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
            private readonly List<string> _addressesToAnalyze;
            private readonly string _chainName;
            private readonly Task<HexBigInteger> _currentBlock;
            private readonly ILog _log;
            private readonly int _maxParallelism;
            public readonly Dictionary<string, Address> Addresses;
            public readonly BlockingCollection<EnTransaction> Transactions;
            public readonly Web3 Web3;


            public ChainData(Web3 web3, string chainName, int maxParallelism, ILog log, List<string> addresses)
            {
                Web3 = web3;
                _chainName = chainName;
                _maxParallelism = maxParallelism;
                _log = log;
                Transactions = new BlockingCollection<EnTransaction>();
                Addresses = new Dictionary<string, Address>();
                _addressesToAnalyze = addresses;
                // Reading current last block processed on chain
                _currentBlock = Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                _currentBlock.Wait();
            }

            public HexBigInteger CurrentBlock => _currentBlock.Result;

            public void GetBlocks(long startBlock, long currentBlock, CancellationToken cancellationToken)
            {
                var totaltrx = 0;
                var blockNum = 1;
                BlockParameter blockParameter;
                Parallel.For(startBlock, currentBlock,
                    new ParallelOptions {MaxDegreeOfParallelism = _maxParallelism}, b =>
                    {
                        // Retrieve Transactions inside block X
                        blockParameter = new BlockParameter((ulong) b);
                        using var block = Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(blockParameter);
                        block.Wait(cancellationToken);
                        Log.Debug(
                            $"[{blockNum.ToString()}/500] block: {b.ToString()}, total trx: {block.Result.Transactions.Length.ToString()}");
                        blockNum++;
                        var txCounter = 0;
                        Parallel.ForEach(block.Result.Transactions, e =>
                        {
                            // Skip if we don't care about the address
                            if (e.To != null &&
                                e.From != null &&
                                !_addressesToAnalyze.Contains(e.From.ToLower()) &&
                                !_addressesToAnalyze.Contains(e.To.ToLower()))
                                return;
                            totaltrx++;
                            var txReceipt = Web3.Eth.Transactions.GetTransactionReceipt
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
                                $"\t{b.ToString()} -> txCounter: {txCounter.ToString()}, txHash: {enTx.Transaction.TransactionHash}, receiptStatus: {enTx.TransactionReceipt.Status}");
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
                            t.Transaction.IsFrom(address) ||
                            t.Transaction.IsTo(address))
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
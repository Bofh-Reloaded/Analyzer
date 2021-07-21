using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Transaction = Nethereum.RPC.Eth.DTOs.Transaction;

namespace AnalyzerCore.Services
{
    public class DataCollectorService : BackgroundService
    {
        private string _chainName;

        // Define the delay between one cycle and another
        private const int TaskDelayMs = 360000;

        // Initialize Logger
        private static readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );

        private readonly Web3 _web3;

        public DataCollectorService(string chainName, string uri)
        {
            _chainName = chainName;
            // Registering Nethereum Web3 client endpoint
            _log.Info($"DataCollectorService Initialized for chain: {chainName}");
            _web3 = new Web3(uri);
        }

        public class ChainData
        {
            private readonly Web3 _web3;
            private readonly Task<HexBigInteger> _currentBlock;
            private readonly string _chainName;
            private readonly int _maxParallelism;
            private readonly ILog _log;
            public HexBigInteger CurrentBlock => _currentBlock.Result;
            public readonly BlockingCollection<EnTransaction> Transactions;
            public Dictionary<string, Address> Addresses;


            public ChainData(Web3 web3, string chainName, int maxParallelism, ILog log)
            {
                _web3 = web3;
                _chainName = chainName;
                _maxParallelism = maxParallelism;
                _log = log;
                Transactions = new BlockingCollection<EnTransaction>();
                Addresses = new Dictionary<string, Address>();
                // Reading current last block processed on chain
                _currentBlock = _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                _currentBlock.Wait();
            }

            public void GetBlocksAsync(long startBlock, long currentBlock)
            {
                Parallel.For(startBlock, currentBlock,
                    new ParallelOptions {MaxDegreeOfParallelism = _maxParallelism}, b =>
                    {
                        _log.Debug($"Processing Block: {b.ToString()}");
                        // Initilize null object to be accessible outside try/catch scope
                        Task<BlockWithTransactions> block = null;
                        // Retrieve Transactions inside block X
                        block = _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                            .SendRequestAsync(new HexBigInteger(b));
                        block.Wait();
                        foreach (var e in block.Result.Transactions)
                        {
                            var enTx = new EnTransaction {Transaction = e, Client = _web3};
                            enTx.GetReceipt();
                            // Filling the blocking collection
                            Transactions.Add(enTx);
                        }
                    });
            }

            public void GetAddressTransactions(string address)
            {
                Addresses[address] = new Address
                {
                    Transactions = Transactions.Where(t => t.Transaction.IsFrom(address) || t.Transaction.IsTo(address))
                        .ToList()
                };
            }

            public class Address
            {
                public List<EnTransaction> Transactions { get; set; }
            }

            public class EnTransaction
            {
                public Web3 Client { get; set; }
                public TransactionReceipt Receipt { get; set; }
                
                public Transaction Transaction { get; set; }
                
                public async Task GetReceipt()
                {
                    Receipt = await Client.Eth.Transactions.GetTransactionReceipt
                            .SendRequestAsync(Transaction.TransactionHash);
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
                _log.Info($"DataCollectorService background task is stopping for chain: {_chainName}"));
            while (!stoppingToken.IsCancellationRequested)
            {
                _log.Info($"Starting a new cycle for chain: {_chainName}");
            }

            await Task.Delay(TaskDelayMs, stoppingToken);
        }
    }
}
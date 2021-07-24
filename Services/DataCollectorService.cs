using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;

namespace AnalyzerCore.Services
{
    public partial class DataCollectorService : BackgroundService
    {
        // Define the delay between one cycle and another
        private const int TaskDelayMs = 360000;

        private const int NumberOfBlocksToRetrieve = 500;

        // Initialize Logger
        private static readonly ILog Log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );

        private readonly int _maxParallelism;

        private readonly Web3 _web3;
        private readonly string _chainName;
        private readonly ChainDataHandler _chainDataHandler;

        public DataCollectorService(string chainName, string uri, int maxParallelism, ChainDataHandler chainDataHandler)
        {
            _chainName = chainName;
            _maxParallelism = maxParallelism;
            _chainDataHandler = chainDataHandler;
            // Registering Web3 client endpoint
            Log.Info($"DataCollectorService Initialized for chain: {chainName}");
            _web3 = new Web3(uri);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
                Log.Info($"DataCollectorService background task is stopping for chain: {_chainName}"));
            while (!stoppingToken.IsCancellationRequested)
            {
                Log.Info($"Starting a new cycle for chain: {_chainName}");
                var chainData = new ChainData(_web3, _chainName, _maxParallelism, Log);
                var currentBlock = chainData.CurrentBlock;
                Log.Info($"Processing Blocks: {NumberOfBlocksToRetrieve.ToString()}");
                var startBlock = new HexBigInteger(currentBlock.Value - NumberOfBlocksToRetrieve);
                Log.Info($"From Block: {startBlock.Value.ToString()} To Block: {currentBlock.Value.ToString()}");
                chainData.GetBlocks((long) startBlock.Value, (long) currentBlock.Value, stoppingToken);
                Log.Info($"Total Trx Retrieved: {chainData.Transactions.Count.ToString()}");
                _chainDataHandler.DataChange(chainData);
                await Task.Delay(TaskDelayMs, stoppingToken);
            }
        }
    }
}
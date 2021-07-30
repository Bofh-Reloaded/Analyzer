using System.Collections.Generic;
using AnalyzerCore.Services;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;

namespace AnalyzerCore.Models
{
    public class ModelsCollection
    {
    }

    public class Options
    {
        public List<string> addresses { get; set; }
        public string ourAddress { get; set; }
    }

    public class BlockRangeStats
    {
        public int BlockRange { get; set; }
        public int SuccededTranstactionsPerBlockRange { get; set; }
        public int TotalTransactionsPerBlockRange { get; set; }
        public string SuccessRate { get; set; }
        public List<DataCollectorService.ChainData.EnTransaction> T0Trx { get; internal set; }
        public List<DataCollectorService.ChainData.EnTransaction> T1Trx { get; internal set; }
        public List<DataCollectorService.ChainData.EnTransaction> T2Trx { get; internal set; }
        public List<DataCollectorService.ChainData.EnTransaction> ContP { get; internal set; }
        public List<DataCollectorService.ChainData.EnTransaction> T0TrxSucceded { get; internal set; }
        public List<DataCollectorService.ChainData.EnTransaction> T1TrxSucceded { get; internal set; }
        public List<DataCollectorService.ChainData.EnTransaction> T2TrxSucceded { get; internal set; }
        public List<DataCollectorService.ChainData.EnTransaction> ContPSucceded { get; internal set; }
    }

    public class AddressStats
    {
        public string Address { get; set; }
        public List<BlockRangeStats> BlockRanges { get; set; }
    }

    public class Message
    {
        public string Timestamp { get; set; }
        public List<AddressStats> Addresses { get; set; }
        public int Tps { get; set; }
        public int TotalTrx { get; internal set; }
        public string OurAddress { get; internal set; }
    }

    public class Token
    {
        [JsonProperty]
        public string TokenAddress { get; set; }
        public string TransactionHash { get; set; }
        public string PoolFactory { get; set; }
        [JsonProperty]
        public string TokenSymbol { get; set; }
        [JsonProperty]
        public string TokenTotalSupply { get; set; }
        public bool IsDeflationary { get; set; }
        public int TxCount { get; set; }
    }
}
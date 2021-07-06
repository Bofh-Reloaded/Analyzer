using System;
using System.Collections;
using System.Collections.Generic;

namespace AnalyzerCore.Models
{
    public class ModelsCollection
    {
        public ModelsCollection()
        {
        }
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
        public int TPS { get; set; }
        public int TotalTrx { get; internal set; }
    }
}

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AnalyzerCore.DbLayer
{
    public class Models
    {
        public class TokenDb
        {
            [Key]
            public string TokenAddress { get; set; }
            public List<TransactionHash> TransactionHashes { get; } = new List<TransactionHash>();
            public List<Exchange> Exchanges { get; } = new List<Exchange>();
            public string TokenSymbol { get; set; }
            public bool IsDeflationary { get; set; }
            public int TxCount { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public List<Pool> Pools = new List<Pool>();
            public int TelegramMsgId { get; set; }
            public bool Notified { get; set; }
        }

        public class TransactionHash
        {
            [Key]
            public int Id { get; set; }
            public string Hash { get; set; }
        }

        public class Exchange
        {
            [Key]
            public int Id { get; set; }
            public string Address { get; set; }
        }

        public class Pool
        {
            [Key]
            public int Id { get; set; }
            public string Address { get; set; }
        }
    }
}
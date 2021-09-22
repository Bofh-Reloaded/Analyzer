using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AnalyzerCore.DbLayer
{
    public partial class Models
    {
        public class TokenEntity
        {
            [Key]
            public int TokenId { get; set; }

            public string TokenAddress { get; set; }

            public string TokenSymbol { get; set; }
            public bool IsDeflationary { get; set; }
            public int TxCount { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public int TelegramMsgId { get; set; }
            public bool Notified { get; set; }
            public bool Deleted { get; set; }
            public ICollection<TransactionHash> TransactionHashes { get; } = new List<TransactionHash>();
            public ICollection<Exchange> Exchanges { get; } = new List<Exchange>();
            public ICollection<Pool> Pools { get; } = new List<Pool>();
        }
    }
}
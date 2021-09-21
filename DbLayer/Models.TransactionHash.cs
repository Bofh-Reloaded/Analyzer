using System.ComponentModel.DataAnnotations;

namespace AnalyzerCore.DbLayer
{
    public partial class Models
    {
        public class TransactionHash
        {
            [Key]
            public int HashId { get; set; }
            public string Hash { get; set; }
            public TokenEntity TokenEntity { get; set; }
        }
    }
}
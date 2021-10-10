using System.ComponentModel.DataAnnotations;

namespace AnalyzerCore.DbLayer
{
    public partial class Models
    {
        public class Exchange
        {
            [Key]
            public int ExchangeId { get; set; }

            public string Address { get; set; }
            public TokenEntity TokenEntity { get; set; }
        }
    }
}
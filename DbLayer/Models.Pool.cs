using System.ComponentModel.DataAnnotations;

namespace AnalyzerCore.DbLayer
{
    public partial class Models
    {
        public class Pool
        {
            [Key]
            public int PoolId { get; set; }
            public string Address { get; set; }
            public TokenEntity TokenEntity { get; set; }
        }
    }
}
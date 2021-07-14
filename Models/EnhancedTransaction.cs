using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;

namespace AnalyzerCore.Models
{
    public class EnhancedTransaction : Transaction
    {
        public string ChainName { get; set; }
        public Task<TransactionReceipt> Receipt { get; set; }
    }
}
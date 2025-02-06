using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Models;

namespace AnalyzerCore.Domain.Services
{
    public interface IBlockchainService
    {
        Task<BigInteger> GetCurrentBlockNumberAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<BlockData>> GetBlocksAsync(BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default);
        Task<TokenInfo> GetTokenInfoAsync(string address, CancellationToken cancellationToken = default);
        Task<PoolInfo> GetPoolInfoAsync(string address, CancellationToken cancellationToken = default);
        Task<(decimal Reserve0, decimal Reserve1)> GetPoolReservesAsync(string address, CancellationToken cancellationToken = default);
        Task<bool> IsContractAsync(string address, CancellationToken cancellationToken = default);
        Task<string> GetContractCreatorAsync(string address, CancellationToken cancellationToken = default);
        Task<IEnumerable<TransactionInfo>> GetTransactionsByAddressAsync(string address, BigInteger fromBlock, BigInteger toBlock, CancellationToken cancellationToken = default);
        Task<decimal> GetTokenBalanceAsync(string tokenAddress, string walletAddress, CancellationToken cancellationToken = default);
    }
}
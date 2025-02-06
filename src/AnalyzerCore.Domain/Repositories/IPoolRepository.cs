using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;

namespace AnalyzerCore.Domain.Repositories
{
    public interface IPoolRepository
    {
        Task<Pool?> GetByAddressAsync(string address, string factory, CancellationToken cancellationToken = default);
        Task<IEnumerable<Pool>> GetAllByFactoryAsync(string factory, CancellationToken cancellationToken = default);
        Task<IEnumerable<Pool>> GetAllByChainIdAsync(string chainId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Pool>> GetPoolsByTokenAsync(string tokenAddress, string chainId, CancellationToken cancellationToken = default);
        Task<Pool> AddAsync(Pool pool, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string address, string factory, CancellationToken cancellationToken = default);
        Task<IEnumerable<Pool>> GetPoolsCreatedAfterAsync(DateTime timestamp, string factory, CancellationToken cancellationToken = default);
        Task UpdateReservesAsync(string address, string factory, decimal reserve0, decimal reserve1, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
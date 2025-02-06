using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using AnalyzerCore.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Infrastructure.Persistence.Repositories
{
    public class PoolRepository : IPoolRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PoolRepository> _logger;

        public PoolRepository(
            ApplicationDbContext context,
            ILogger<PoolRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Pool> GetByAddressAsync(
            string address,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Pools
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .FirstOrDefaultAsync(p => 
                    p.Address.ToLower() == address.ToLower() && 
                    p.Token0.ChainId == chainId,
                    cancellationToken);
        }

        public async Task<IEnumerable<Pool>> GetAllByChainIdAsync(
            string chainId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Pools
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .Where(p => p.Token0.ChainId == chainId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Pool> AddAsync(Pool pool, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Adding pool {Address} to chain {ChainId}",
                pool.Address,
                pool.Token0.ChainId);

            var entry = await _context.Pools.AddAsync(pool, cancellationToken);
            return entry.Entity;
        }

        public async Task<bool> ExistsAsync(
            string address,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Pools
                .AnyAsync(p => 
                    p.Address.ToLower() == address.ToLower() && 
                    p.Token0.ChainId == chainId,
                    cancellationToken);
        }

        public async Task<IEnumerable<Pool>> GetPoolsCreatedAfterAsync(
            DateTime timestamp,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Pools
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .Where(p => 
                    p.CreatedAt >= timestamp &&
                    p.Token0.ChainId == chainId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Pool>> GetPoolsByTokenAsync(
            string tokenAddress,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Pools
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .Where(p => 
                    (p.Token0.Address.ToLower() == tokenAddress.ToLower() ||
                     p.Token1.Address.ToLower() == tokenAddress.ToLower()) &&
                    p.Token0.ChainId == chainId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateReservesAsync(
            string poolAddress,
            string chainId,
            decimal reserve0,
            decimal reserve1,
            CancellationToken cancellationToken = default)
        {
            var pool = await GetByAddressAsync(poolAddress, chainId, cancellationToken);
            if (pool != null)
            {
                pool.UpdateReserves(reserve0, reserve1);
                _context.Pools.Update(pool);
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving pool changes to database");
                throw;
            }
        }
    }
}
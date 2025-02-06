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

        public async Task<Pool?> GetByAddressAsync(
            string address,
            string factory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(factory))
            {
                return null;
            }

            var normalizedAddress = address.ToLowerInvariant();
            var normalizedFactory = factory.ToLowerInvariant();

            return await _context.Pools
                .AsNoTracking()
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .FirstOrDefaultAsync(p => 
                    p.Address == normalizedAddress && 
                    p.Factory == normalizedFactory,
                    cancellationToken);
        }

        public async Task<IEnumerable<Pool>> GetAllByFactoryAsync(
            string factory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(factory))
            {
                return Array.Empty<Pool>();
            }

            var normalizedFactory = factory.ToLowerInvariant();

            return await _context.Pools
                .AsNoTracking()
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .Where(p => p.Factory == normalizedFactory)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Pool>> GetAllByChainIdAsync(
            string chainId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(chainId))
            {
                return Array.Empty<Pool>();
            }

            return await _context.Pools
                .AsNoTracking()
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .Where(p => p.Token0.ChainId == chainId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Pool>> GetPoolsByTokenAsync(
            string tokenAddress,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tokenAddress) || string.IsNullOrEmpty(chainId))
            {
                return Array.Empty<Pool>();
            }

            var normalizedAddress = tokenAddress.ToLowerInvariant();

            return await _context.Pools
                .AsNoTracking()
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .Where(p => 
                    (p.Token0.Address == normalizedAddress || p.Token1.Address == normalizedAddress) &&
                    p.Token0.ChainId == chainId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Pool> AddAsync(Pool pool, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Adding pool {Address} ({Token0}/{Token1}) from factory {Factory}",
                pool.Address,
                pool.Token0?.Symbol ?? "unknown",
                pool.Token1?.Symbol ?? "unknown",
                pool.Factory);

            var entry = await _context.Pools.AddAsync(pool, cancellationToken);
            return entry.Entity;
        }

        public async Task<bool> ExistsAsync(
            string address,
            string factory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(factory))
            {
                return false;
            }

            var normalizedAddress = address.ToLowerInvariant();
            var normalizedFactory = factory.ToLowerInvariant();

            return await _context.Pools
                .AsNoTracking()
                .AnyAsync(p => 
                    p.Address == normalizedAddress && 
                    p.Factory == normalizedFactory,
                    cancellationToken);
        }

        public async Task<IEnumerable<Pool>> GetPoolsCreatedAfterAsync(
            DateTime timestamp,
            string factory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(factory))
            {
                return Array.Empty<Pool>();
            }

            var normalizedFactory = factory.ToLowerInvariant();

            return await _context.Pools
                .AsNoTracking()
                .Include(p => p.Token0)
                .Include(p => p.Token1)
                .Where(p => 
                    p.CreatedAt >= timestamp &&
                    p.Factory == normalizedFactory)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateReservesAsync(
            string address,
            string factory,
            decimal reserve0,
            decimal reserve1,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(factory))
            {
                return;
            }

            var normalizedAddress = address.ToLowerInvariant();
            var normalizedFactory = factory.ToLowerInvariant();

            var pool = await _context.Pools
                .FirstOrDefaultAsync(p =>
                    p.Address == normalizedAddress &&
                    p.Factory == normalizedFactory,
                    cancellationToken);

            if (pool != null)
            {
                pool.Reserve0 = reserve0;
                pool.Reserve1 = reserve1;
                pool.LastUpdated = DateTime.UtcNow;

                _logger.LogInformation(
                    "Updated reserves for pool {Address}: {Reserve0}/{Reserve1}",
                    address,
                    reserve0,
                    reserve1);
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
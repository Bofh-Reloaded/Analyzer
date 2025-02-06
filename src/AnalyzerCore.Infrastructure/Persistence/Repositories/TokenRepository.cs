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
    public class TokenRepository : ITokenRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TokenRepository> _logger;

        public TokenRepository(
            ApplicationDbContext context,
            ILogger<TokenRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Token?> GetByAddressAsync(
            string address,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(chainId))
            {
                return null;
            }

            var normalizedAddress = address.ToLowerInvariant();
            return await _context.Tokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => 
                    t.Address == normalizedAddress && 
                    t.ChainId == chainId,
                    cancellationToken);
        }

        public async Task<IEnumerable<Token>> GetAllByChainIdAsync(
            string chainId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Tokens
                .AsNoTracking()
                .Where(t => t.ChainId == chainId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Token> AddAsync(Token token, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Adding token {Symbol} ({Address}) to chain {ChainId}",
                token.Symbol,
                token.Address,
                token.ChainId);

            var entry = await _context.Tokens.AddAsync(token, cancellationToken);
            return entry.Entity;
        }

        public async Task<bool> ExistsAsync(
            string address,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(chainId))
            {
                return false;
            }

            var normalizedAddress = address.ToLowerInvariant();
            return await _context.Tokens
                .AsNoTracking()
                .AnyAsync(t => 
                    t.Address == normalizedAddress && 
                    t.ChainId == chainId,
                    cancellationToken);
        }

        public async Task<IEnumerable<Token>> GetTokensCreatedAfterAsync(
            DateTime timestamp,
            string chainId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Tokens
                .AsNoTracking()
                .Where(t => 
                    t.CreatedAt >= timestamp &&
                    t.ChainId == chainId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving token changes to database");
                throw;
            }
        }
    }
}
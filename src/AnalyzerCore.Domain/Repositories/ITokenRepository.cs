using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;

namespace AnalyzerCore.Domain.Repositories
{
    public interface ITokenRepository
    {
        Task<Token?> GetByAddressAsync(string address, string chainId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Token>> GetAllByChainIdAsync(string chainId, CancellationToken cancellationToken = default);
        Task<Token> AddAsync(Token token, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string address, string chainId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Token>> GetTokensCreatedAfterAsync(DateTime timestamp, string chainId, CancellationToken cancellationToken = default);
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
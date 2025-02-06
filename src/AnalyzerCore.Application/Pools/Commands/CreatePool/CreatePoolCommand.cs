using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using AnalyzerCore.Domain.Repositories;
using AnalyzerCore.Domain.Services;
using AnalyzerCore.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Application.Pools.Commands.CreatePool
{
    public record CreatePoolCommand : IRequest<Pool>
    {
        public string Address { get; init; }
        public string Token0Address { get; init; }
        public string Token1Address { get; init; }
        public string Factory { get; init; }
        public PoolType Type { get; init; }
        public string ChainId { get; init; }
    }

    public class CreatePoolCommandHandler : IRequestHandler<CreatePoolCommand, Pool>
    {
        private readonly IPoolRepository _poolRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IBlockchainService _blockchainService;
        private readonly ILogger<CreatePoolCommandHandler> _logger;

        public CreatePoolCommandHandler(
            IPoolRepository poolRepository,
            ITokenRepository tokenRepository,
            IBlockchainService blockchainService,
            ILogger<CreatePoolCommandHandler> logger)
        {
            _poolRepository = poolRepository;
            _tokenRepository = tokenRepository;
            _blockchainService = blockchainService;
            _logger = logger;
        }

        public async Task<Pool> Handle(CreatePoolCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Creating pool {Address} on chain {ChainId}",
                request.Address,
                request.ChainId);

            // Get or create tokens
            var token0 = await GetOrCreateTokenAsync(request.Token0Address, request.ChainId, cancellationToken);
            var token1 = await GetOrCreateTokenAsync(request.Token1Address, request.ChainId, cancellationToken);

            // Get initial reserves
            var (reserve0, reserve1) = await _blockchainService.GetPoolReservesAsync(request.Address, cancellationToken);

            var pool = Pool.Create(
                request.Address,
                token0,
                token1,
                reserve0,
                reserve1,
                request.Type,
                request.Factory);

            var exists = await _poolRepository.ExistsAsync(request.Address, request.ChainId, cancellationToken);
            if (!exists)
            {
                await _poolRepository.AddAsync(pool, cancellationToken);
                await _poolRepository.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation(
                    "Created pool {Address} on chain {ChainId}",
                    pool.Address,
                    request.ChainId);
            }
            else
            {
                _logger.LogInformation(
                    "Pool {Address} already exists on chain {ChainId}",
                    request.Address,
                    request.ChainId);
                
                pool = await _poolRepository.GetByAddressAsync(request.Address, request.ChainId, cancellationToken);
            }

            return pool;
        }

        private async Task<Token> GetOrCreateTokenAsync(string address, string chainId, CancellationToken cancellationToken)
        {
            var token = await _tokenRepository.GetByAddressAsync(address, chainId, cancellationToken);
            if (token == null)
            {
                var tokenInfo = await _blockchainService.GetTokenInfoAsync(address, cancellationToken);
                token = Token.Create(
                    address,
                    tokenInfo.Symbol,
                    tokenInfo.Name,
                    tokenInfo.Decimals,
                    chainId);
                
                await _tokenRepository.AddAsync(token, cancellationToken);
                await _tokenRepository.SaveChangesAsync(cancellationToken);
            }
            return token;
        }
    }
}
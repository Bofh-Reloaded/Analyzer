using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using AnalyzerCore.Domain.Repositories;
using AnalyzerCore.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Application.Pools.Commands.CreatePool
{
    public class CreatePoolCommand : IRequest<Pool?>
    {
        public string Address { get; set; } = null!;
        public string Token0Address { get; set; } = null!;
        public string Token1Address { get; set; } = null!;
        public string Factory { get; set; } = null!;
        public PoolType Type { get; set; } = PoolType.UniswapV2;
        public string ChainId { get; set; } = null!;
    }

    public class CreatePoolCommandHandler : IRequestHandler<CreatePoolCommand, Pool?>
    {
        private readonly IPoolRepository _poolRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly ILogger<CreatePoolCommandHandler> _logger;

        public CreatePoolCommandHandler(
            IPoolRepository poolRepository,
            ITokenRepository tokenRepository,
            ILogger<CreatePoolCommandHandler> logger)
        {
            _poolRepository = poolRepository;
            _tokenRepository = tokenRepository;
            _logger = logger;
        }

        public async Task<Pool?> Handle(CreatePoolCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Address) || 
                string.IsNullOrEmpty(request.Token0Address) || 
                string.IsNullOrEmpty(request.Token1Address) ||
                string.IsNullOrEmpty(request.Factory) ||
                string.IsNullOrEmpty(request.ChainId))
            {
                _logger.LogError(
                    "Invalid pool creation request. Missing required fields: Address={Address}, Token0={Token0}, Token1={Token1}, Factory={Factory}, ChainId={ChainId}",
                    request.Address,
                    request.Token0Address,
                    request.Token1Address,
                    request.Factory,
                    request.ChainId);
                return null;
            }

            if (await _poolRepository.ExistsAsync(request.Address, request.Factory, cancellationToken))
            {
                _logger.LogInformation(
                    "Pool {Address} already exists on factory {Factory}",
                    request.Address,
                    request.Factory);

                return await _poolRepository.GetByAddressAsync(request.Address, request.Factory, cancellationToken);
            }

            var token0 = await GetOrCreateTokenAsync(request.Token0Address, request.ChainId, cancellationToken);
            var token1 = await GetOrCreateTokenAsync(request.Token1Address, request.ChainId, cancellationToken);

            if (token0 == null || token1 == null)
            {
                _logger.LogError(
                    "Failed to create or retrieve tokens for pool {Address}",
                    request.Address);
                return null;
            }

            var pool = Pool.Create(
                request.Address,
                token0,
                token1,
                request.Factory,
                request.Type);

            _logger.LogInformation(
                "Creating pool {Address} ({Token0}/{Token1}) on factory {Factory}",
                request.Address,
                token0.Symbol,
                token1.Symbol,
                request.Factory);

            await _poolRepository.AddAsync(pool, cancellationToken);
            await _poolRepository.SaveChangesAsync(cancellationToken);

            return pool;
        }

        private async Task<Token?> GetOrCreateTokenAsync(
            string address,
            string chainId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(chainId))
            {
                _logger.LogError(
                    "Invalid token address or chainId: Address={Address}, ChainId={ChainId}",
                    address,
                    chainId);
                return null;
            }

            var token = await _tokenRepository.GetByAddressAsync(address, chainId, cancellationToken);
            if (token != null)
            {
                return token;
            }

            _logger.LogInformation(
                "Token {Address} not found, creating placeholder",
                address);

            token = Token.Create(
                address,
                "UNKNOWN",
                "Unknown Token",
                18,
                0,
                chainId);

            await _tokenRepository.AddAsync(token, cancellationToken);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            return token;
        }
    }
}
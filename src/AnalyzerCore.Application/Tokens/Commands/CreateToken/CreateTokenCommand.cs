using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using AnalyzerCore.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Application.Tokens.Commands.CreateToken
{
    public class CreateTokenCommand : IRequest<Token?>
    {
        public string Address { get; set; } = null!;
        public string Symbol { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Decimals { get; set; }
        public decimal TotalSupply { get; set; }
        public string ChainId { get; set; } = null!;
    }

    public class CreateTokenCommandHandler : IRequestHandler<CreateTokenCommand, Token?>
    {
        private readonly ITokenRepository _tokenRepository;
        private readonly ILogger<CreateTokenCommandHandler> _logger;

        public CreateTokenCommandHandler(
            ITokenRepository tokenRepository,
            ILogger<CreateTokenCommandHandler> logger)
        {
            _tokenRepository = tokenRepository;
            _logger = logger;
        }

        public async Task<Token?> Handle(CreateTokenCommand request, CancellationToken cancellationToken)
        {
            if (await _tokenRepository.ExistsAsync(request.Address, request.ChainId, cancellationToken))
            {
                _logger.LogInformation(
                    "Token {Symbol} ({Address}) already exists on chain {ChainId}",
                    request.Symbol,
                    request.Address,
                    request.ChainId);

                return await _tokenRepository.GetByAddressAsync(request.Address, request.ChainId, cancellationToken);
            }

            var token = Token.Create(
                request.Address,
                request.Symbol,
                request.Name,
                request.Decimals,
                request.TotalSupply,
                request.ChainId);

            _logger.LogInformation(
                "Creating token {Symbol} ({Address}) on chain {ChainId}",
                request.Symbol,
                request.Address,
                request.ChainId);

            await _tokenRepository.AddAsync(token, cancellationToken);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            return token;
        }
    }
}
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using AnalyzerCore.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Application.Tokens.Commands.CreateToken
{
    public record CreateTokenCommand : IRequest<Token>
    {
        public string Address { get; init; }
        public string Symbol { get; init; }
        public string Name { get; init; }
        public int Decimals { get; init; }
        public string ChainId { get; init; }
    }

    public class CreateTokenCommandHandler : IRequestHandler<CreateTokenCommand, Token>
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

        public async Task<Token> Handle(CreateTokenCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Creating token {Symbol} ({Address}) on chain {ChainId}",
                request.Symbol,
                request.Address,
                request.ChainId);

            var token = Token.Create(
                request.Address,
                request.Symbol,
                request.Name,
                request.Decimals,
                request.ChainId);

            var exists = await _tokenRepository.ExistsAsync(request.Address, request.ChainId, cancellationToken);
            if (!exists)
            {
                await _tokenRepository.AddAsync(token, cancellationToken);
                await _tokenRepository.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation(
                    "Created token {Symbol} ({Address}) on chain {ChainId}",
                    token.Symbol,
                    token.Address,
                    token.ChainId);
            }
            else
            {
                _logger.LogInformation(
                    "Token {Address} already exists on chain {ChainId}",
                    request.Address,
                    request.ChainId);
                
                token = await _tokenRepository.GetByAddressAsync(request.Address, request.ChainId, cancellationToken);
            }

            return token;
        }
    }
}
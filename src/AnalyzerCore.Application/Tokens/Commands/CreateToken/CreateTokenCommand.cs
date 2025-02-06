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
            var token = Token.Create(
                request.Address,
                request.Symbol,
                request.Name,
                request.Decimals,
                request.ChainId);

            if (!await _tokenRepository.ExistsAsync(request.Address, request.ChainId, cancellationToken))
            {
                await _tokenRepository.AddAsync(token, cancellationToken);
                await _tokenRepository.SaveChangesAsync(cancellationToken);
            }
            else
            {
                token = await _tokenRepository.GetByAddressAsync(request.Address, request.ChainId, cancellationToken);
            }

            return token;
        }
    }
}
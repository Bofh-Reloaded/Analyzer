using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using AnalyzerCore.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Application.Tokens.Queries.GetTokens
{
    public record GetTokensQuery : IRequest<IEnumerable<Token>>
    {
        public string ChainId { get; init; }
        public DateTime? CreatedAfter { get; init; }
    }

    public class GetTokensQueryHandler : IRequestHandler<GetTokensQuery, IEnumerable<Token>>
    {
        private readonly ITokenRepository _tokenRepository;
        private readonly ILogger<GetTokensQueryHandler> _logger;

        public GetTokensQueryHandler(
            ITokenRepository tokenRepository,
            ILogger<GetTokensQueryHandler> logger)
        {
            _tokenRepository = tokenRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<Token>> Handle(GetTokensQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Getting tokens for chain {ChainId}{CreatedAfter}",
                request.ChainId,
                request.CreatedAfter.HasValue ? $" created after {request.CreatedAfter}" : "");

            if (request.CreatedAfter.HasValue)
            {
                return await _tokenRepository.GetTokensCreatedAfterAsync(
                    request.CreatedAfter.Value,
                    request.ChainId,
                    cancellationToken);
            }

            return await _tokenRepository.GetAllByChainIdAsync(request.ChainId, cancellationToken);
        }
    }
}
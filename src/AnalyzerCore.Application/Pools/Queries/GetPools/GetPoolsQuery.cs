using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Entities;
using AnalyzerCore.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Application.Pools.Queries.GetPools
{
    public record GetPoolsQuery : IRequest<IEnumerable<Pool>>
    {
        public string ChainId { get; init; }
        public DateTime? CreatedAfter { get; init; }
        public string TokenAddress { get; init; }
    }

    public class GetPoolsQueryHandler : IRequestHandler<GetPoolsQuery, IEnumerable<Pool>>
    {
        private readonly IPoolRepository _poolRepository;
        private readonly ILogger<GetPoolsQueryHandler> _logger;

        public GetPoolsQueryHandler(
            IPoolRepository poolRepository,
            ILogger<GetPoolsQueryHandler> logger)
        {
            _poolRepository = poolRepository;
            _logger = logger;
        }

        public async Task<IEnumerable<Pool>> Handle(GetPoolsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Getting pools for chain {ChainId}{CreatedAfter}{TokenAddress}",
                request.ChainId,
                request.CreatedAfter.HasValue ? $" created after {request.CreatedAfter}" : "",
                !string.IsNullOrEmpty(request.TokenAddress) ? $" containing token {request.TokenAddress}" : "");

            if (!string.IsNullOrEmpty(request.TokenAddress))
            {
                return await _poolRepository.GetPoolsByTokenAsync(
                    request.TokenAddress,
                    request.ChainId,
                    cancellationToken);
            }

            if (request.CreatedAfter.HasValue)
            {
                return await _poolRepository.GetPoolsCreatedAfterAsync(
                    request.CreatedAfter.Value,
                    request.ChainId,
                    cancellationToken);
            }

            return await _poolRepository.GetAllByChainIdAsync(request.ChainId, cancellationToken);
        }
    }
}
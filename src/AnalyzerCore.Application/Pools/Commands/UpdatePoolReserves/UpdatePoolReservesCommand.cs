using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Application.Pools.Commands.UpdatePoolReserves
{
    public record UpdatePoolReservesCommand : IRequest<Unit>
    {
        public string Address { get; init; }
        public string ChainId { get; init; }
        public decimal Reserve0 { get; init; }
        public decimal Reserve1 { get; init; }
    }

    public class UpdatePoolReservesCommandHandler : IRequestHandler<UpdatePoolReservesCommand, Unit>
    {
        private readonly IPoolRepository _poolRepository;
        private readonly ILogger<UpdatePoolReservesCommandHandler> _logger;

        public UpdatePoolReservesCommandHandler(
            IPoolRepository poolRepository,
            ILogger<UpdatePoolReservesCommandHandler> logger)
        {
            _poolRepository = poolRepository;
            _logger = logger;
        }

        public async Task<Unit> Handle(UpdatePoolReservesCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Updating reserves for pool {Address} on chain {ChainId}",
                request.Address,
                request.ChainId);

            await _poolRepository.UpdateReservesAsync(
                request.Address,
                request.ChainId,
                request.Reserve0,
                request.Reserve1,
                cancellationToken);

            await _poolRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated reserves for pool {Address} on chain {ChainId}: {Reserve0}, {Reserve1}",
                request.Address,
                request.ChainId,
                request.Reserve0,
                request.Reserve1);

            return Unit.Value;
        }
    }
}
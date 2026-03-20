using MediatR;
using Witness.Application.DTOs;

namespace Witness.Application.Queries;

public sealed record InspectInteractionQuery : IRequest<InteractionDto?>
{
    public string WitnessId { get; init; } = string.Empty;
    public string? SessionId { get; init; }
}

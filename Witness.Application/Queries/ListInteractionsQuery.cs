using MediatR;
using Witness.Application.DTOs;

namespace Witness.Application.Queries;

public sealed record ListInteractionsQuery : IRequest<ListInteractionsResult>
{
    public string SessionId { get; init; } = string.Empty;
    public int Limit { get; init; } = 50;
}

public sealed record ListInteractionsResult
{
    public string SessionId { get; init; } = string.Empty;
    public int Count { get; init; }
    public int Total { get; init; }
    public List<InteractionSummaryDto> Interactions { get; init; } = new();
}

public sealed record InteractionSummaryDto
{
    public string WitnessId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public List<string> Tags { get; init; } = new();
}

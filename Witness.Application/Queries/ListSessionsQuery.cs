using MediatR;
using Witness.Application.DTOs;

namespace Witness.Application.Queries;

public sealed record ListSessionsQuery : IRequest<ListSessionsResult>
{
    public int Limit { get; init; } = 50;
}

public sealed record ListSessionsResult
{
    public int Count { get; init; }
    public int Total { get; init; }
    public List<SessionDto> Sessions { get; init; } = new();
}

using MediatR;
using Witness.Application.DTOs;

namespace Witness.Application.Commands;

public sealed record RecordInteractionCommand : IRequest<RecordInteractionResult>
{
    public string Target { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public Dictionary<string, string>? Headers { get; init; }
    public object? Body { get; init; }
    public RecordOptions? Options { get; init; }
}

public sealed record RecordOptions
{
    public string? Tag { get; init; }
    public string? SessionId { get; init; }
    public string? Description { get; init; }
    public int? TimeoutMs { get; init; }
    public bool? FollowRedirects { get; init; }
}

public sealed record RecordInteractionResult
{
    public string WitnessId { get; init; } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public object? ResponseBody { get; init; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = new();
    public bool Stored { get; init; }
}

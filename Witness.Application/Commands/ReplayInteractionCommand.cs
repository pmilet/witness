using MediatR;

namespace Witness.Application.Commands;

public sealed record ReplayInteractionCommand : IRequest<ReplayInteractionResult>
{
    public string WitnessId { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public ReplayOptions? Options { get; init; }
}

public sealed record ReplayOptions
{
    public string? Tag { get; init; }
    public string? SessionId { get; init; }
    public Dictionary<string, string>? OverrideHeaders { get; init; }
}

public sealed record ReplayInteractionResult
{
    public string OriginalWitnessId { get; init; } = string.Empty;
    public string ReplayWitnessId { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public object? ResponseBody { get; init; }
    public bool Stored { get; init; }
}

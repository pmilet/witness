namespace Witness.Application.DTOs;

/// <summary>
/// DTO for replaying HTTP interactions
/// </summary>
public sealed record ReplayRequestDto
{
    public required string WitnessId { get; init; }
    public required string Target { get; init; }
    public ReplayOptionsDto? Options { get; init; }
}

public sealed record ReplayOptionsDto
{
    public string Tag { get; init; } = "replay";
    public string? SessionId { get; init; }
    public Dictionary<string, string>? OverrideHeaders { get; init; }
}

public sealed record ReplayResponseDto
{
    public required string OriginalWitnessId { get; init; }
    public required string ReplayWitnessId { get; init; }
    public required int StatusCode { get; init; }
    public required long DurationMs { get; init; }
    public object? ResponseBody { get; init; }
    public bool Stored { get; init; }
}

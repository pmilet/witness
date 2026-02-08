namespace Witness.Application.DTOs;

/// <summary>
/// DTO for recording HTTP interactions
/// </summary>
public sealed record RecordRequestDto
{
    public required string Target { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public string? Body { get; init; }
    public RecordOptionsDto? Options { get; init; }
}

public sealed record RecordOptionsDto
{
    public string Tag { get; init; } = "default";
    public string SessionId { get; init; } = "default-session";
    public bool FollowRedirects { get; init; } = true;
    public int TimeoutMs { get; init; } = 30000;
}

public sealed record RecordResponseDto
{
    public required string WitnessId { get; init; }
    public required string SessionId { get; init; }
    public required int StatusCode { get; init; }
    public required long DurationMs { get; init; }
    public object? ResponseBody { get; init; }
    public Dictionary<string, string>? ResponseHeaders { get; init; }
    public bool Stored { get; init; }
}

using Witness.Domain.Entities;

namespace Witness.AspNetCore;

/// <summary>
/// Witness operation mode for the current request.
/// </summary>
public enum WitnessMode
{
    /// <summary>No Witness interception — pass through normally.</summary>
    None,
    /// <summary>Record mode — outbound calls are executed and captured.</summary>
    Record,
    /// <summary>Replay mode — outbound calls return previously recorded responses.</summary>
    Replay
}

/// <summary>
/// Flows Witness correlation context through the async call chain using AsyncLocal.
/// In Record mode, outbound calls are collected in CapturedOutbound.
/// In Replay mode, outbound calls are served from the PlaybackQueue.
/// </summary>
public sealed class WitnessCallContext
{
    private static readonly AsyncLocal<WitnessCallContext?> _current = new();

    /// <summary>Gets or sets the current context for the async flow.</summary>
    public static WitnessCallContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public WitnessMode Mode { get; init; }
    public string? CorrelationId { get; init; }

    /// <summary>Record mode: outbound interactions collected here during request processing.</summary>
    public List<Interaction> CapturedOutbound { get; } = new();

    /// <summary>Replay mode: recorded outbound interactions to serve back in order.</summary>
    public Queue<Interaction> PlaybackQueue { get; } = new();

    public static WitnessCallContext CreateForRecord(string correlationId) =>
        new() { Mode = WitnessMode.Record, CorrelationId = correlationId };

    public static WitnessCallContext CreateForReplay(string correlationId, IEnumerable<Interaction> outboundCalls)
    {
        var ctx = new WitnessCallContext { Mode = WitnessMode.Replay, CorrelationId = correlationId };
        foreach (var call in outboundCalls)
            ctx.PlaybackQueue.Enqueue(call);
        return ctx;
    }
}

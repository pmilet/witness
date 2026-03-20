using Witness.Domain.ValueObjects;

namespace Witness.Domain.Entities;

/// <summary>
/// Aggregate root representing a recorded HTTP interaction
/// </summary>
public sealed class Interaction
{
    public WitnessId Id { get; private set; }
    public string SessionId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public HttpRequest Request { get; private set; }
    public HttpResponse Response { get; private set; }
    public InteractionMetadata Metadata { get; private set; }
    public IReadOnlyList<Interaction>? OutboundCalls { get; private set; }

    // For EF Core or serialization
    private Interaction()
    {
        Id = null!;
        SessionId = string.Empty;
        Request = null!;
        Response = null!;
        Metadata = null!;
    }

    private Interaction(
        WitnessId id,
        string sessionId,
        DateTime timestamp,
        HttpRequest request,
        HttpResponse response,
        InteractionMetadata metadata,
        IReadOnlyList<Interaction>? outboundCalls = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        Timestamp = timestamp;
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Response = response ?? throw new ArgumentNullException(nameof(response));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        OutboundCalls = outboundCalls;
    }

    /// <summary>
    /// Create a new Interaction from its components
    /// </summary>
    public static Interaction Create(
        WitnessId id,
        string sessionId,
        HttpRequest request,
        HttpResponse response,
        InteractionMetadata? metadata = null,
        IReadOnlyList<Interaction>? outboundCalls = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        return new Interaction(
            id,
            sessionId,
            DateTime.UtcNow,
            request,
            response,
            metadata ?? new InteractionMetadata(),
            outboundCalls);
    }

    /// <summary>
    /// Recreate an Interaction with a specific timestamp (for loading from storage)
    /// </summary>
    public static Interaction Recreate(
        WitnessId id,
        string sessionId,
        DateTime timestamp,
        HttpRequest request,
        HttpResponse response,
        InteractionMetadata metadata,
        IReadOnlyList<Interaction>? outboundCalls = null)
    {
        return new Interaction(id, sessionId, timestamp, request, response, metadata, outboundCalls);
    }
}

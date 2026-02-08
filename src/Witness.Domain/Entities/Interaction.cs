using Witness.Domain.ValueObjects;

namespace Witness.Domain.Entities;

/// <summary>
/// Core entity representing a recorded HTTP interaction
/// </summary>
public sealed class Interaction
{
    public WitnessId WitnessId { get; private set; }
    public string SessionId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public HttpRequest Request { get; private set; }
    public HttpResponse Response { get; private set; }
    public InteractionMetadata Metadata { get; private set; }

    private Interaction(
        WitnessId witnessId,
        string sessionId,
        DateTimeOffset timestamp,
        HttpRequest request,
        HttpResponse response,
        InteractionMetadata metadata)
    {
        WitnessId = witnessId;
        SessionId = sessionId;
        Timestamp = timestamp;
        Request = request;
        Response = response;
        Metadata = metadata;
    }

    public static Interaction Create(
        WitnessId witnessId,
        string sessionId,
        DateTimeOffset timestamp,
        HttpRequest request,
        HttpResponse response,
        InteractionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(witnessId, nameof(witnessId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        ArgumentNullException.ThrowIfNull(response, nameof(response));
        ArgumentNullException.ThrowIfNull(metadata, nameof(metadata));

        return new Interaction(witnessId, sessionId, timestamp, request, response, metadata);
    }
}

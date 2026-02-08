namespace Witness.Domain.Entities;

/// <summary>
/// Represents a session that groups related interactions
/// </summary>
public sealed class Session
{
    public string SessionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Description { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; }

    private readonly List<string> _interactionIds = new();
    public IReadOnlyList<string> InteractionIds => _interactionIds.AsReadOnly();

    private Session(
        string sessionId,
        DateTimeOffset createdAt,
        string? description,
        IReadOnlyList<string> tags)
    {
        SessionId = sessionId;
        CreatedAt = createdAt;
        Description = description;
        Tags = tags;
    }

    public static Session Create(
        string sessionId,
        DateTimeOffset createdAt,
        string? description = null,
        List<string>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));

        return new Session(
            sessionId,
            createdAt,
            description,
            tags?.AsReadOnly() ?? new List<string>().AsReadOnly());
    }

    public void AddInteraction(string witnessId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(witnessId, nameof(witnessId));

        if (!_interactionIds.Contains(witnessId))
        {
            _interactionIds.Add(witnessId);
        }
    }
}

namespace Witness.Domain.Entities;

/// <summary>
/// Represents a session that groups related interactions
/// </summary>
public sealed class Session
{
    public string SessionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; }
    public int InteractionCount { get; private set; }
    public string? Description { get; private set; }

    // For EF Core or serialization
    private Session()
    {
        SessionId = string.Empty;
        Tags = new List<string>();
    }

    private Session(string sessionId, DateTime createdAt, IReadOnlyList<string> tags, int interactionCount, string? description)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        CreatedAt = createdAt;
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        InteractionCount = interactionCount;
        Description = description;
    }

    public static Session Create(string sessionId, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        return new Session(sessionId, DateTime.UtcNow, new List<string>(), 0, description);
    }

    public static Session Recreate(string sessionId, DateTime createdAt, IReadOnlyList<string> tags, int interactionCount, string? description = null)
    {
        return new Session(sessionId, createdAt, tags, interactionCount, description);
    }

    public void IncrementInteractionCount()
    {
        InteractionCount++;
    }

    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

        if (!Tags.Contains(tag))
        {
            var tagsList = Tags.ToList();
            tagsList.Add(tag);
            Tags = tagsList;
        }
    }

    public void AddTags(IEnumerable<string> tags)
    {
        foreach (var tag in tags)
        {
            AddTag(tag);
        }
    }
}

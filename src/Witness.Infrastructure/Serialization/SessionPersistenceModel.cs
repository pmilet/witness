using Witness.Domain.Entities;

namespace Witness.Infrastructure.Serialization;

/// <summary>
/// Persistence model for Session serialization
/// </summary>
public sealed class SessionPersistenceModel
{
    public string SessionId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> InteractionIds { get; set; } = new();

    public static SessionPersistenceModel FromDomain(Session session)
    {
        return new SessionPersistenceModel
        {
            SessionId = session.SessionId,
            CreatedAt = session.CreatedAt.ToString("o"),
            Description = session.Description,
            Tags = session.Tags.ToList(),
            InteractionIds = session.InteractionIds.ToList()
        };
    }

    public Session ToDomain()
    {
        var session = Session.Create(
            SessionId,
            DateTimeOffset.Parse(CreatedAt),
            Description,
            Tags);

        foreach (var interactionId in InteractionIds)
        {
            session.AddInteraction(interactionId);
        }

        return session;
    }
}

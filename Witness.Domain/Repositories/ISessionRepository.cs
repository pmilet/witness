using Witness.Domain.Entities;

namespace Witness.Domain.Repositories;

/// <summary>
/// Repository interface for managing Session entities
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Save or update a session
    /// </summary>
    Task SaveAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a session by its ID
    /// </summary>
    Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all sessions
    /// </summary>
    Task<IReadOnlyList<Session>> ListAsync(int limit = 50, CancellationToken cancellationToken = default);
}

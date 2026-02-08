using Witness.Domain.Entities;

namespace Witness.Domain.Repositories;

/// <summary>
/// Repository for Session persistence
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Saves a session to storage
    /// </summary>
    Task SaveAsync(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a session by its ID
    /// </summary>
    Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all sessions
    /// </summary>
    Task<IReadOnlyList<Session>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a session exists
    /// </summary>
    Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);
}

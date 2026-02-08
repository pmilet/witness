using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;

namespace Witness.Domain.Repositories;

/// <summary>
/// Repository interface for managing Interaction aggregate roots
/// </summary>
public interface IInteractionRepository
{
    /// <summary>
    /// Save an interaction to storage
    /// </summary>
    Task SaveAsync(Interaction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load an interaction by its WitnessId
    /// </summary>
    Task<Interaction?> GetByIdAsync(WitnessId witnessId, string? sessionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all interactions in a session
    /// </summary>
    Task<IReadOnlyList<Interaction>> ListBySessionAsync(string sessionId, int limit = 50, CancellationToken cancellationToken = default);
}

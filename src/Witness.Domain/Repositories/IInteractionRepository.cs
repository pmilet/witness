using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;

namespace Witness.Domain.Repositories;

/// <summary>
/// Repository for Interaction persistence
/// </summary>
public interface IInteractionRepository
{
    /// <summary>
    /// Saves an interaction to storage
    /// </summary>
    Task SaveAsync(Interaction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an interaction by its WitnessId
    /// </summary>
    Task<Interaction?> GetByIdAsync(WitnessId witnessId, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all interactions in a session
    /// </summary>
    Task<IReadOnlyList<Interaction>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an interaction exists
    /// </summary>
    Task<bool> ExistsAsync(WitnessId witnessId, string sessionId, CancellationToken cancellationToken = default);
}

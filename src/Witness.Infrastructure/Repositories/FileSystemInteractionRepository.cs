using System.Text.Json;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Domain.ValueObjects;
using Witness.Infrastructure.Serialization;

namespace Witness.Infrastructure.Repositories;

/// <summary>
/// File system based interaction repository
/// </summary>
public sealed class FileSystemInteractionRepository : IInteractionRepository
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemInteractionRepository(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath, nameof(storagePath));
        _storagePath = storagePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction, nameof(interaction));

        var sessionPath = GetSessionPath(interaction.SessionId);
        var interactionsPath = Path.Combine(sessionPath, "interactions");
        Directory.CreateDirectory(interactionsPath);

        var filePath = Path.Combine(interactionsPath, $"{interaction.WitnessId.Value}.json");
        var model = InteractionPersistenceModel.FromDomain(interaction);
        var json = JsonSerializer.Serialize(model, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public async Task<Interaction?> GetByIdAsync(WitnessId witnessId, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(witnessId, nameof(witnessId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));

        var filePath = Path.Combine(GetSessionPath(sessionId), "interactions", $"{witnessId.Value}.json");

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var model = JsonSerializer.Deserialize<InteractionPersistenceModel>(json, _jsonOptions);

        return model?.ToDomain();
    }

    public async Task<IReadOnlyList<Interaction>> ListBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));

        var interactionsPath = Path.Combine(GetSessionPath(sessionId), "interactions");

        if (!Directory.Exists(interactionsPath))
        {
            return Array.Empty<Interaction>();
        }

        var files = Directory.GetFiles(interactionsPath, "*.json");
        var interactions = new List<Interaction>();

        foreach (var file in files)
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            var model = JsonSerializer.Deserialize<InteractionPersistenceModel>(json, _jsonOptions);

            if (model != null)
            {
                interactions.Add(model.ToDomain());
            }
        }

        return interactions;
    }

    public Task<bool> ExistsAsync(WitnessId witnessId, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(witnessId, nameof(witnessId));
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));

        var filePath = Path.Combine(GetSessionPath(sessionId), "interactions", $"{witnessId.Value}.json");
        return Task.FromResult(File.Exists(filePath));
    }

    private string GetSessionPath(string sessionId)
    {
        return Path.Combine(_storagePath, "sessions", sessionId);
    }
}

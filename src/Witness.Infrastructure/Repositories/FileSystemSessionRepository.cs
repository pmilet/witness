using System.Text.Json;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Infrastructure.Serialization;

namespace Witness.Infrastructure.Repositories;

/// <summary>
/// File system based session repository
/// </summary>
public sealed class FileSystemSessionRepository : ISessionRepository
{
    private readonly string _storagePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemSessionRepository(string storagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath, nameof(storagePath));
        _storagePath = storagePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));

        var sessionPath = GetSessionPath(session.SessionId);
        Directory.CreateDirectory(sessionPath);

        var filePath = Path.Combine(sessionPath, "session.json");
        var model = SessionPersistenceModel.FromDomain(session);
        var json = JsonSerializer.Serialize(model, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    public async Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));

        var filePath = Path.Combine(GetSessionPath(sessionId), "session.json");

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var model = JsonSerializer.Deserialize<SessionPersistenceModel>(json, _jsonOptions);

        return model?.ToDomain();
    }

    public async Task<IReadOnlyList<Session>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var sessionsPath = Path.Combine(_storagePath, "sessions");

        if (!Directory.Exists(sessionsPath))
        {
            return Array.Empty<Session>();
        }

        var sessionDirs = Directory.GetDirectories(sessionsPath);
        var sessions = new List<Session>();

        foreach (var dir in sessionDirs)
        {
            var filePath = Path.Combine(dir, "session.json");
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var model = JsonSerializer.Deserialize<SessionPersistenceModel>(json, _jsonOptions);

                if (model != null)
                {
                    sessions.Add(model.ToDomain());
                }
            }
        }

        return sessions;
    }

    public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));

        var filePath = Path.Combine(GetSessionPath(sessionId), "session.json");
        return Task.FromResult(File.Exists(filePath));
    }

    private string GetSessionPath(string sessionId)
    {
        return Path.Combine(_storagePath, "sessions", sessionId);
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Persistence;

namespace Witness.Infrastructure.Repositories;

public sealed class FileSystemSessionRepository : ISessionRepository
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemSessionRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemSessionRepository(
        IOptions<WitnessOptions> options,
        ILogger<FileSystemSessionRepository> logger)
    {
        _basePath = options.Value.Storage.Path;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public async Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        var sessionPath = Path.Combine(_basePath, "sessions", session.SessionId);
        Directory.CreateDirectory(sessionPath);

        var filePath = Path.Combine(sessionPath, "session.json");

        var model = new SessionModel
        {
            SessionId = session.SessionId,
            CreatedAt = session.CreatedAt,
            Tags = new List<string>(session.Tags),
            InteractionCount = session.InteractionCount,
            Description = session.Description
        };

        var json = JsonSerializer.Serialize(model, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogInformation("Saved session: {SessionId}", session.SessionId);
    }

    public async Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_basePath, "sessions", sessionId, "session.json");

        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var model = JsonSerializer.Deserialize<SessionModel>(json);

            if (model == null) return null;

            return Session.Recreate(
                model.SessionId,
                model.CreatedAt,
                model.Tags,
                model.InteractionCount,
                model.Description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<IReadOnlyList<Session>> ListAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var sessionsPath = Path.Combine(_basePath, "sessions");

        if (!Directory.Exists(sessionsPath))
            return Array.Empty<Session>();

        var sessionDirs = Directory.GetDirectories(sessionsPath)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .Take(limit);

        var sessions = new List<Session>();

        foreach (var dir in sessionDirs)
        {
            var sessionFile = Path.Combine(dir, "session.json");
            if (!File.Exists(sessionFile)) continue;

            try
            {
                var json = await File.ReadAllTextAsync(sessionFile, cancellationToken);
                var model = JsonSerializer.Deserialize<SessionModel>(json);

                if (model != null)
                {
                    sessions.Add(Session.Recreate(
                        model.SessionId,
                        model.CreatedAt,
                        model.Tags,
                        model.InteractionCount,
                        model.Description));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session from {Dir}", dir);
            }
        }

        return sessions.OrderByDescending(s => s.CreatedAt).ToList();
    }
}

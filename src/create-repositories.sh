#!/bin/bash
set -e

# Persistence Models
cat > Witness.Infrastructure/Persistence/InteractionModel.cs << 'EOF'
namespace Witness.Infrastructure.Persistence;

public sealed class InteractionModel
{
    public string WitnessId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public HttpRequestModel Request { get; set; } = null!;
    public HttpResponseModel Response { get; set; } = null!;
    public InteractionMetadataModel Metadata { get; set; } = null!;
}

public sealed class HttpRequestModel
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public object? Body { get; set; }
    public string? ContentType { get; set; }
}

public sealed class HttpResponseModel
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public object? Body { get; set; }
    public string? ContentType { get; set; }
    public long DurationMs { get; set; }
}

public sealed class InteractionMetadataModel
{
    public List<string> Tags { get; set; } = new();
    public string? Description { get; set; }
    public string? OpenApiOperationId { get; set; }
    public int? ChainStep { get; set; }
    public string? ChainId { get; set; }
}
EOF

cat > Witness.Infrastructure/Persistence/SessionModel.cs << 'EOF'
namespace Witness.Infrastructure.Persistence;

public sealed class SessionModel
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public int InteractionCount { get; set; }
    public string? Description { get; set; }
}
EOF

# Interaction Repository
cat > Witness.Infrastructure/Repositories/FileSystemInteractionRepository.cs << 'EOF'
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Domain.ValueObjects;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Persistence;

namespace Witness.Infrastructure.Repositories;

public sealed class FileSystemInteractionRepository : IInteractionRepository
{
    private readonly string _basePath;
    private readonly ILogger<FileSystemInteractionRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileSystemInteractionRepository(
        IOptions<WitnessOptions> options,
        ILogger<FileSystemInteractionRepository> logger)
    {
        _basePath = options.Value.Storage.Path;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        
        EnsureDirectoriesExist();
    }

    public async Task SaveAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        var sessionPath = GetSessionPath(interaction.SessionId);
        var interactionsPath = Path.Combine(sessionPath, "interactions");
        Directory.CreateDirectory(interactionsPath);

        var filePath = Path.Combine(interactionsPath, $"{interaction.Id.Value}.json");

        var model = MapToModel(interaction);
        var json = JsonSerializer.Serialize(model, _jsonOptions);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        
        _logger.LogInformation("Saved interaction: {WitnessId} to {FilePath}", interaction.Id.Value, filePath);
    }

    public async Task<Interaction?> GetByIdAsync(WitnessId witnessId, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        if (sessionId != null)
        {
            var interaction = await LoadFromSessionAsync(witnessId, sessionId, cancellationToken);
            if (interaction != null) return interaction;
        }

        // Search all sessions
        var sessionsPath = Path.Combine(_basePath, "sessions");
        if (!Directory.Exists(sessionsPath)) return null;

        foreach (var sessionDir in Directory.GetDirectories(sessionsPath))
        {
            var sessionIdFromPath = Path.GetFileName(sessionDir);
            var interaction = await LoadFromSessionAsync(witnessId, sessionIdFromPath, cancellationToken);
            if (interaction != null) return interaction;
        }

        return null;
    }

    public async Task<IReadOnlyList<Interaction>> ListBySessionAsync(string sessionId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var interactionsPath = Path.Combine(GetSessionPath(sessionId), "interactions");
        
        if (!Directory.Exists(interactionsPath))
            return Array.Empty<Interaction>();

        var files = Directory.GetFiles(interactionsPath, "*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(limit);

        var interactions = new List<Interaction>();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var model = JsonSerializer.Deserialize<InteractionModel>(json);
                if (model != null)
                {
                    interactions.Add(MapFromModel(model));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load interaction from {File}", file);
            }
        }

        return interactions;
    }

    private async Task<Interaction?> LoadFromSessionAsync(WitnessId witnessId, string sessionId, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(GetSessionPath(sessionId), "interactions", $"{witnessId.Value}.json");

        if (!File.Exists(filePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var model = JsonSerializer.Deserialize<InteractionModel>(json);
            return model != null ? MapFromModel(model) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load interaction {WitnessId} from {SessionId}", witnessId.Value, sessionId);
            return null;
        }
    }

    private string GetSessionPath(string sessionId)
    {
        return Path.Combine(_basePath, "sessions", sessionId);
    }

    private void EnsureDirectoriesExist()
    {
        var sessionsPath = Path.Combine(_basePath, "sessions");
        Directory.CreateDirectory(sessionsPath);
    }

    private static InteractionModel MapToModel(Interaction interaction)
    {
        return new InteractionModel
        {
            WitnessId = interaction.Id.Value,
            SessionId = interaction.SessionId,
            Timestamp = interaction.Timestamp,
            Request = new HttpRequestModel
            {
                Method = interaction.Request.Method,
                Url = interaction.Request.Url,
                Path = interaction.Request.Path,
                Headers = new Dictionary<string, string>(interaction.Request.Headers),
                Body = interaction.Request.Body,
                ContentType = interaction.Request.ContentType
            },
            Response = new HttpResponseModel
            {
                StatusCode = interaction.Response.StatusCode,
                Headers = new Dictionary<string, string>(interaction.Response.Headers),
                Body = interaction.Response.Body,
                ContentType = interaction.Response.ContentType,
                DurationMs = interaction.Response.DurationMs
            },
            Metadata = new InteractionMetadataModel
            {
                Tags = new List<string>(interaction.Metadata.Tags),
                Description = interaction.Metadata.Description,
                OpenApiOperationId = interaction.Metadata.OpenApiOperationId,
                ChainStep = interaction.Metadata.ChainStep,
                ChainId = interaction.Metadata.ChainId
            }
        };
    }

    private static Interaction MapFromModel(InteractionModel model)
    {
        var witnessId = WitnessId.Parse(model.WitnessId);
        
        var request = new HttpRequest(
            model.Request.Method,
            model.Request.Url,
            model.Request.Path,
            model.Request.Headers,
            model.Request.Body,
            model.Request.ContentType);

        var response = new HttpResponse(
            model.Response.StatusCode,
            model.Response.Headers,
            model.Response.Body,
            model.Response.ContentType,
            model.Response.DurationMs);

        var metadata = new InteractionMetadata(
            model.Metadata.Tags,
            model.Metadata.Description,
            model.Metadata.OpenApiOperationId,
            model.Metadata.ChainStep,
            model.Metadata.ChainId);

        return Interaction.Recreate(
            witnessId,
            model.SessionId,
            model.Timestamp,
            request,
            response,
            metadata);
    }
}
EOF

# Session Repository
cat > Witness.Infrastructure/Repositories/FileSystemSessionRepository.cs << 'EOF'
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
EOF

echo "Repositories created successfully!"

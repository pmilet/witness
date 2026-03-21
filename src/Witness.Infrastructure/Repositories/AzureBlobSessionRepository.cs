using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Persistence;

namespace Witness.Infrastructure.Repositories;

public sealed class AzureBlobSessionRepository : ISessionRepository
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobSessionRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AzureBlobSessionRepository(
        BlobServiceClient blobServiceClient,
        IOptions<WitnessOptions> options,
        ILogger<AzureBlobSessionRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var containerName = options.Value.Storage.ContainerName;
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public async Task SaveAsync(Session session, CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = GetSessionBlobName(session.SessionId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var model = new SessionModel
        {
            SessionId = session.SessionId,
            CreatedAt = session.CreatedAt,
            Tags = new List<string>(session.Tags),
            InteractionCount = session.InteractionCount,
            Description = session.Description
        };

        var json = JsonSerializer.Serialize(model, _jsonOptions);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        _logger.LogInformation("Saved session: {SessionId}", session.SessionId);
    }

    public async Task<Session?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var blobName = GetSessionBlobName(sessionId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            if (!await blobClient.ExistsAsync(cancellationToken)) return null;

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var json = response.Value.Content.ToString();
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
        var sessions = new List<(DateTimeOffset LastModified, Session Session)>();

        await foreach (var blobItem in _containerClient.GetBlobsAsync(
            prefix: "sessions/",
            traits: Azure.Storage.Blobs.Models.BlobTraits.None,
            states: Azure.Storage.Blobs.Models.BlobStates.None,
            cancellationToken: cancellationToken))
        {
            if (!blobItem.Name.EndsWith("/session.json", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                var json = response.Value.Content.ToString();
                var model = JsonSerializer.Deserialize<SessionModel>(json);

                if (model != null)
                {
                    var lastModified = blobItem.Properties.LastModified ?? DateTimeOffset.MinValue;
                    sessions.Add((lastModified, Session.Recreate(
                        model.SessionId,
                        model.CreatedAt,
                        model.Tags,
                        model.InteractionCount,
                        model.Description)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load session from blob {BlobName}", blobItem.Name);
            }
        }

        return sessions
            .OrderByDescending(x => x.LastModified)
            .Take(limit)
            .OrderByDescending(x => x.Session.CreatedAt)
            .Select(x => x.Session)
            .ToList();
    }

    private static string GetSessionBlobName(string sessionId)
        => $"sessions/{sessionId}/session.json";
}

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Domain.ValueObjects;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Persistence;

namespace Witness.Infrastructure.Repositories;

public sealed class AzureBlobInteractionRepository : IInteractionRepository
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobInteractionRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AzureBlobInteractionRepository(
        BlobServiceClient blobServiceClient,
        IOptions<WitnessOptions> options,
        ILogger<AzureBlobInteractionRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(blobServiceClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var containerName = options.Value.Storage.ContainerName;
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public async Task SaveAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = GetInteractionBlobName(interaction.SessionId, interaction.Id.Value);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var model = MapToModel(interaction);
        var json = JsonSerializer.Serialize(model, _jsonOptions);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        _logger.LogInformation("Saved interaction: {WitnessId} to blob {BlobName}", interaction.Id.Value, blobName);
    }

    public async Task<Interaction?> GetByIdAsync(WitnessId witnessId, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        if (sessionId != null)
        {
            var interaction = await LoadFromSessionAsync(witnessId, sessionId, cancellationToken);
            if (interaction != null) return interaction;
        }

        // Search all sessions
        await foreach (var blobHierarchyItem in _containerClient.GetBlobsByHierarchyAsync(
            delimiter: "/",
            prefix: "sessions/",
            traits: Azure.Storage.Blobs.Models.BlobTraits.None,
            states: Azure.Storage.Blobs.Models.BlobStates.None,
            cancellationToken: cancellationToken))
        {
            if (!blobHierarchyItem.IsPrefix) continue;

            // Extract sessionId from "sessions/{sessionId}/"
            var prefix = blobHierarchyItem.Prefix;
            var sessionIdFromPath = prefix.Split('/', StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            if (sessionIdFromPath == null) continue;

            if (sessionId != null && sessionIdFromPath == sessionId) continue; // Already checked above

            var interaction = await LoadFromSessionAsync(witnessId, sessionIdFromPath, cancellationToken);
            if (interaction != null) return interaction;
        }

        return null;
    }

    public async Task<IReadOnlyList<Interaction>> ListBySessionAsync(string sessionId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var prefix = $"sessions/{sessionId}/interactions/";
        var interactions = new List<(DateTimeOffset LastModified, Interaction Interaction)>();

        await foreach (var blobItem in _containerClient.GetBlobsAsync(
            prefix: prefix,
            traits: Azure.Storage.Blobs.Models.BlobTraits.None,
            states: Azure.Storage.Blobs.Models.BlobStates.None,
            cancellationToken: cancellationToken))
        {
            if (!blobItem.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                var json = response.Value.Content.ToString();
                var model = JsonSerializer.Deserialize<InteractionModel>(json);
                if (model != null)
                {
                    var lastModified = blobItem.Properties.LastModified ?? DateTimeOffset.MinValue;
                    interactions.Add((lastModified, MapFromModel(model)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load interaction from blob {BlobName}", blobItem.Name);
            }
        }

        return interactions
            .OrderByDescending(x => x.LastModified)
            .Take(limit)
            .Select(x => x.Interaction)
            .ToList();
    }

    private async Task<Interaction?> LoadFromSessionAsync(WitnessId witnessId, string sessionId, CancellationToken cancellationToken)
    {
        var blobName = GetInteractionBlobName(sessionId, witnessId.Value);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            if (!await blobClient.ExistsAsync(cancellationToken)) return null;

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var json = response.Value.Content.ToString();
            var model = JsonSerializer.Deserialize<InteractionModel>(json);
            return model != null ? MapFromModel(model) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load interaction {WitnessId} from session {SessionId}", witnessId.Value, sessionId);
            return null;
        }
    }

    private static string GetInteractionBlobName(string sessionId, string witnessId)
        => $"sessions/{sessionId}/interactions/{witnessId}.json";

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
            },
            OutboundCalls = interaction.OutboundCalls?
                .Select(MapToModel)
                .ToList()
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

        var outboundCalls = model.OutboundCalls?
            .Select(MapFromModel)
            .ToList();

        return Interaction.Recreate(
            witnessId,
            model.SessionId,
            model.Timestamp,
            request,
            response,
            metadata,
            outboundCalls);
    }
}

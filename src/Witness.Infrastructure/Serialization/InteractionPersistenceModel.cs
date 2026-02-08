using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;

namespace Witness.Infrastructure.Serialization;

/// <summary>
/// Persistence model for Interaction serialization
/// </summary>
public sealed class InteractionPersistenceModel
{
    public string WitnessId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public RequestModel Request { get; set; } = new();
    public ResponseModel Response { get; set; } = new();
    public MetadataModel Metadata { get; set; } = new();

    public static InteractionPersistenceModel FromDomain(Interaction interaction)
    {
        return new InteractionPersistenceModel
        {
            WitnessId = interaction.WitnessId.Value,
            SessionId = interaction.SessionId,
            Timestamp = interaction.Timestamp.ToString("o"),
            Request = new RequestModel
            {
                Method = interaction.Request.Method.Value,
                Url = interaction.Request.Url,
                Path = interaction.Request.Path,
                Headers = interaction.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Body = interaction.Request.Body,
                ContentType = interaction.Request.ContentType
            },
            Response = new ResponseModel
            {
                StatusCode = interaction.Response.StatusCode,
                Headers = interaction.Response.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Body = interaction.Response.Body,
                ContentType = interaction.Response.ContentType,
                DurationMs = interaction.Response.DurationMs
            },
            Metadata = new MetadataModel
            {
                Tags = interaction.Metadata.Tags.ToList(),
                Description = interaction.Metadata.Description,
                OpenApiOperationId = interaction.Metadata.OpenApiOperationId,
                ChainStep = interaction.Metadata.ChainStep,
                ChainId = interaction.Metadata.ChainId
            }
        };
    }

    public Interaction ToDomain()
    {
        var witnessId = Domain.ValueObjects.WitnessId.Parse(WitnessId);
        var httpMethod = Domain.ValueObjects.HttpMethod.FromString(Request.Method);

        var httpRequest = HttpRequest.Create(
            httpMethod,
            Request.Url,
            Request.Path,
            Request.Headers,
            Request.Body,
            Request.ContentType);

        var httpResponse = HttpResponse.Create(
            Response.StatusCode,
            Response.Headers,
            Response.Body,
            Response.ContentType,
            Response.DurationMs);

        var metadata = InteractionMetadata.Create(
            Metadata.Tags,
            Metadata.Description,
            Metadata.OpenApiOperationId,
            Metadata.ChainStep,
            Metadata.ChainId);

        return Interaction.Create(
            witnessId,
            SessionId,
            DateTimeOffset.Parse(Timestamp),
            httpRequest,
            httpResponse,
            metadata);
    }
}

public sealed class RequestModel
{
    public string Method { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public string? ContentType { get; set; }
}

public sealed class ResponseModel
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public string? ContentType { get; set; }
    public long DurationMs { get; set; }
}

public sealed class MetadataModel
{
    public List<string> Tags { get; set; } = new();
    public string? Description { get; set; }
    public string? OpenApiOperationId { get; set; }
    public int? ChainStep { get; set; }
    public string? ChainId { get; set; }
}

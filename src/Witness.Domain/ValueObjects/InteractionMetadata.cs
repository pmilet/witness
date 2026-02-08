namespace Witness.Domain.ValueObjects;

/// <summary>
/// Metadata about an interaction
/// </summary>
public sealed record InteractionMetadata
{
    public IReadOnlyList<string> Tags { get; init; }
    public string? Description { get; init; }
    public string? OpenApiOperationId { get; init; }
    public int? ChainStep { get; init; }
    public string? ChainId { get; init; }

    private InteractionMetadata(
        IReadOnlyList<string> tags,
        string? description,
        string? openApiOperationId,
        int? chainStep,
        string? chainId)
    {
        Tags = tags;
        Description = description;
        OpenApiOperationId = openApiOperationId;
        ChainStep = chainStep;
        ChainId = chainId;
    }

    public static InteractionMetadata Create(
        List<string>? tags = null,
        string? description = null,
        string? openApiOperationId = null,
        int? chainStep = null,
        string? chainId = null)
    {
        return new InteractionMetadata(
            tags?.AsReadOnly() ?? new List<string>().AsReadOnly(),
            description,
            openApiOperationId,
            chainStep,
            chainId);
    }
}

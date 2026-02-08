namespace Witness.Domain.Entities;

/// <summary>
/// Metadata associated with an interaction
/// </summary>
public sealed class InteractionMetadata
{
    public IReadOnlyList<string> Tags { get; private set; }
    public string? Description { get; private set; }
    public string? OpenApiOperationId { get; private set; }
    public int? ChainStep { get; private set; }
    public string? ChainId { get; private set; }

    public InteractionMetadata(
        IEnumerable<string>? tags = null,
        string? description = null,
        string? openApiOperationId = null,
        int? chainStep = null,
        string? chainId = null)
    {
        Tags = (tags ?? Enumerable.Empty<string>()).ToList();
        Description = description;
        OpenApiOperationId = openApiOperationId;
        ChainStep = chainStep;
        ChainId = chainId;
    }

    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

        if (!Tags.Contains(tag))
        {
            var tagsList = Tags.ToList();
            tagsList.Add(tag);
            Tags = tagsList;
        }
    }
}

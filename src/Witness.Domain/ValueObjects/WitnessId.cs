namespace Witness.Domain.ValueObjects;

/// <summary>
/// Unique identifier for a recorded interaction
/// Format: {tag}_{method}_{path-slug}_{body-hash}_{timestamp}
/// </summary>
public sealed record WitnessId
{
    public string Value { get; init; }
    public string Tag { get; init; }
    public string Method { get; init; }
    public string PathSlug { get; init; }
    public string BodyHash { get; init; }
    public string Timestamp { get; init; }

    private WitnessId(string value, string tag, string method, string pathSlug, string bodyHash, string timestamp)
    {
        Value = value;
        Tag = tag;
        Method = method;
        PathSlug = pathSlug;
        BodyHash = bodyHash;
        Timestamp = timestamp;
    }

    public static WitnessId Create(string tag, string method, string pathSlug, string bodyHash, string timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, nameof(tag));
        ArgumentException.ThrowIfNullOrWhiteSpace(method, nameof(method));
        ArgumentException.ThrowIfNullOrWhiteSpace(pathSlug, nameof(pathSlug));
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyHash, nameof(bodyHash));
        ArgumentException.ThrowIfNullOrWhiteSpace(timestamp, nameof(timestamp));

        var value = $"{tag}_{method}_{pathSlug}_{bodyHash}_{timestamp}";
        return new WitnessId(value, tag, method, pathSlug, bodyHash, timestamp);
    }

    public static WitnessId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var parts = value.Split('_');
        if (parts.Length != 5)
        {
            throw new ArgumentException($"Invalid WitnessId format: {value}. Expected format: tag_method_path-slug_body-hash_timestamp", nameof(value));
        }

        return new WitnessId(value, parts[0], parts[1], parts[2], parts[3], parts[4]);
    }

    public override string ToString() => Value;
}

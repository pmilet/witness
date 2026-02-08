using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Witness.Domain.ValueObjects;

/// <summary>
/// Deterministic, human-readable identifier for HTTP interactions
/// Format: {tag}_{method}_{path-slug}_{body-hash}_{timestamp}
/// </summary>
public sealed class WitnessId : IEquatable<WitnessId>
{
    private const int MaxPathSlugLength = 60;
    private const int BodyHashLength = 8;

    public string Value { get; }
    public string Tag { get; }
    public string Method { get; }
    public string PathSlug { get; }
    public string BodyHash { get; }
    public string Timestamp { get; }

    private WitnessId(string value, string tag, string method, string pathSlug, string bodyHash, string timestamp)
    {
        Value = value;
        Tag = tag;
        Method = method;
        PathSlug = pathSlug;
        BodyHash = bodyHash;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Generate a new WitnessId from request components
    /// </summary>
    public static WitnessId Generate(string tag, string method, string path, object? body = null)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be null or empty", nameof(tag));
        
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be null or empty", nameof(method));
        
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        var pathSlug = CreatePathSlug(path);
        var bodyHash = CreateBodyHash(body);
        var timestamp = CreateTimestamp();

        var value = $"{tag}_{method.ToUpperInvariant()}_{pathSlug}_{bodyHash}_{timestamp}";
        
        return new WitnessId(value, tag, method.ToUpperInvariant(), pathSlug, bodyHash, timestamp);
    }

    /// <summary>
    /// Parse an existing WitnessId from its string representation
    /// </summary>
    public static WitnessId Parse(string witnessId)
    {
        if (string.IsNullOrWhiteSpace(witnessId))
            throw new ArgumentException("WitnessId cannot be null or empty", nameof(witnessId));

        var parts = witnessId.Split('_');
        if (parts.Length < 5)
            throw new FormatException($"Invalid WitnessId format: {witnessId}");

        // Reconstruct components (path slug may contain underscores)
        var tag = parts[0];
        var method = parts[1];
        var timestamp = parts[^1];
        var bodyHash = parts[^2];
        var pathSlug = string.Join('_', parts.Skip(2).Take(parts.Length - 4));

        return new WitnessId(witnessId, tag, method, pathSlug, bodyHash, timestamp);
    }

    private static string CreatePathSlug(string path)
    {
        // Remove leading slash and convert remaining slashes to hyphens
        var slug = path.TrimStart('/').Replace('/', '-');

        // Remove query parameters
        var queryIndex = slug.IndexOf('?');
        if (queryIndex >= 0)
            slug = slug[..queryIndex];

        // Replace special characters with hyphens
        slug = Regex.Replace(slug, @"[^a-zA-Z0-9-]", "-");

        // Remove consecutive hyphens
        slug = Regex.Replace(slug, @"-+", "-");

        // Truncate to max length
        if (slug.Length > MaxPathSlugLength)
            slug = slug[..MaxPathSlugLength];

        // Remove trailing hyphen
        slug = slug.TrimEnd('-');

        return string.IsNullOrEmpty(slug) ? "root" : slug;
    }

    private static string CreateBodyHash(object? body)
    {
        if (body is null)
            return "00000000";

        string bodyString;
        if (body is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return "00000000";
            bodyString = str;
        }
        else
        {
            bodyString = System.Text.Json.JsonSerializer.Serialize(body);
            if (bodyString == "{}" || bodyString == "[]")
                return "00000000";
        }

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(bodyString));
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return hashHex[..BodyHashLength];
    }

    private static string CreateTimestamp()
    {
        var now = DateTime.UtcNow;
        return $"{now:yyyyMMdd}T{now:HHmm}";
    }

    public bool Equals(WitnessId? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as WitnessId);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;

    public static bool operator ==(WitnessId? left, WitnessId? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(WitnessId? left, WitnessId? right) =>
        !(left == right);

    public static implicit operator string(WitnessId witnessId) => witnessId.Value;
}

using System.Security.Cryptography;
using System.Text;
using Witness.Domain.ValueObjects;

namespace Witness.Domain.Services;

/// <summary>
/// Default implementation of WitnessId generation
/// </summary>
public sealed class WitnessIdGenerator : IWitnessIdGenerator
{
    public WitnessId Generate(string tag, string method, string path, string? body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag, nameof(tag));
        ArgumentException.ThrowIfNullOrWhiteSpace(method, nameof(method));
        ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

        var pathSlug = GeneratePathSlug(path);
        var bodyHash = GenerateBodyHash(body);
        var timestamp = GenerateTimestamp();

        return WitnessId.Create(tag, method, pathSlug, bodyHash, timestamp);
    }

    private static string GeneratePathSlug(string path)
    {
        var slug = path.TrimStart('/').Replace('/', '-');
        if (slug.Length > 60)
        {
            slug = slug[..60];
        }
        return slug;
    }

    private static string GenerateBodyHash(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "00000000";
        }

        var bytes = Encoding.UTF8.GetBytes(body);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    private static string GenerateTimestamp()
    {
        return DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmm");
    }
}

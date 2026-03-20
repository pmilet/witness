namespace Witness.Proxy;

/// <summary>
/// Configuration for the Witness recording proxy.
/// </summary>
public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    /// <summary>Port the proxy listens on.</summary>
    public int Port { get; set; } = 9999;

    /// <summary>Upstream base URL all requests are forwarded to (e.g. http://legacy:3001).</summary>
    public string Upstream { get; set; } = string.Empty;

    /// <summary>Session name under which all captured interactions are stored.</summary>
    public string SessionId { get; set; } = $"proxy-session-{DateTime.UtcNow:yyyy-MM-dd}";

    /// <summary>Tag prefix embedded in the WitnessId of each captured interaction.</summary>
    public string Tag { get; set; } = "proxy";

    /// <summary>Root path of the witness-store directory.</summary>
    public string StorePath { get; set; } = "./witness-store";
}

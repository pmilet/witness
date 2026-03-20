namespace Witness.AspNetCore;

/// <summary>
/// Options for the WitnessCaptureHandler DelegatingHandler.
/// </summary>
public sealed class WitnessCaptureOptions
{
    /// <summary>Session name under which all captured interactions are stored.</summary>
    public string SessionId { get; set; } = $"session-{DateTime.UtcNow:yyyy-MM-dd}";

    /// <summary>Tag prefix embedded in the WitnessId of each captured interaction.</summary>
    public string Tag { get; set; } = "outbound";

    /// <summary>Root path of the witness-store directory.</summary>
    public string StorePath { get; set; } = "./witness-store";
}

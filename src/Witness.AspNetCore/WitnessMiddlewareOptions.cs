namespace Witness.AspNetCore;

/// <summary>
/// Options for the WitnessMiddleware.
/// </summary>
public sealed class WitnessMiddlewareOptions
{
    /// <summary>Root path of the witness-store directory used by the middleware.</summary>
    public string StorePath { get; set; } = "./witness-store";
}

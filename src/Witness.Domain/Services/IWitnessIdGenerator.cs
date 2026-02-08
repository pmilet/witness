using Witness.Domain.ValueObjects;

namespace Witness.Domain.Services;

/// <summary>
/// Service for generating WitnessId values
/// </summary>
public interface IWitnessIdGenerator
{
    /// <summary>
    /// Generates a WitnessId from request components
    /// </summary>
    WitnessId Generate(string tag, string method, string path, string? body);
}

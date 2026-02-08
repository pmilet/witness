namespace Witness.Infrastructure.Configuration;

public sealed class WitnessOptions
{
    public const string SectionName = "Witness";

    public StorageOptions Storage { get; init; } = new();
    public DefaultsOptions Defaults { get; init; } = new();
}

public sealed class StorageOptions
{
    public string Type { get; init; } = "local";
    public string Path { get; init; } = "./witness-store";
}

public sealed class DefaultsOptions
{
    public int TimeoutMs { get; init; } = 30000;
    public bool FollowRedirects { get; init; } = true;
}

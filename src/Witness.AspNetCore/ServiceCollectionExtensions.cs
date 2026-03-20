using Microsoft.Extensions.DependencyInjection;

namespace Witness.AspNetCore;

/// <summary>
/// Extension methods for registering Witness capture on IHttpClientBuilder.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the WitnessCaptureHandler to the named HttpClient pipeline.
    /// Every outbound call made by that client is recorded to witness-store.
    /// </summary>
    public static IHttpClientBuilder AddWitnessCapture(
        this IHttpClientBuilder builder,
        Action<WitnessCaptureOptions>? configure = null)
    {
        var options = new WitnessCaptureOptions();
        configure?.Invoke(options);

        return builder.AddHttpMessageHandler(() => new WitnessCaptureHandler(options));
    }
}

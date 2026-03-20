using Microsoft.AspNetCore.Builder;
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

    /// <summary>
    /// Adds the Witness middleware to the request pipeline.
    /// This enables record/replay of inbound requests with outbound call capture.
    /// </summary>
    public static IApplicationBuilder UseWitnessMiddleware(
        this IApplicationBuilder app,
        Action<WitnessMiddlewareOptions>? configure = null)
    {
        var options = new WitnessMiddlewareOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<WitnessMiddleware>(options);
    }
}

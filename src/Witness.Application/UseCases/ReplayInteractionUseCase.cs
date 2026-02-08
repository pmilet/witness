using System.Text.Json;
using Witness.Application.DTOs;
using Witness.Application.Interfaces;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Domain.Services;
using Witness.Domain.ValueObjects;

namespace Witness.Application.UseCases;

/// <summary>
/// Use case for replaying HTTP interactions
/// </summary>
public sealed class ReplayInteractionUseCase
{
    private readonly IHttpExecutor _httpExecutor;
    private readonly IWitnessIdGenerator _witnessIdGenerator;
    private readonly IInteractionRepository _interactionRepository;
    private readonly ISessionRepository _sessionRepository;

    public ReplayInteractionUseCase(
        IHttpExecutor httpExecutor,
        IWitnessIdGenerator witnessIdGenerator,
        IInteractionRepository interactionRepository,
        ISessionRepository sessionRepository)
    {
        _httpExecutor = httpExecutor ?? throw new ArgumentNullException(nameof(httpExecutor));
        _witnessIdGenerator = witnessIdGenerator ?? throw new ArgumentNullException(nameof(witnessIdGenerator));
        _interactionRepository = interactionRepository ?? throw new ArgumentNullException(nameof(interactionRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<ReplayResponseDto> ExecuteAsync(ReplayRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var witnessId = WitnessId.Parse(request.WitnessId);
        var options = request.Options ?? new ReplayOptionsDto();

        var originalSessionId = options.SessionId ?? "default-session";
        var originalInteraction = await _interactionRepository.GetByIdAsync(witnessId, originalSessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Interaction with WitnessId {request.WitnessId} not found in session {originalSessionId}");

        var headers = originalInteraction.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (options.OverrideHeaders != null)
        {
            foreach (var (key, value) in options.OverrideHeaders)
            {
                headers[key] = value;
            }
        }

        var fullUrl = CombineUrl(request.Target, originalInteraction.Request.Path);

        var httpRequest = HttpRequest.Create(
            originalInteraction.Request.Method,
            fullUrl,
            originalInteraction.Request.Path,
            headers,
            originalInteraction.Request.Body,
            originalInteraction.Request.ContentType);

        var httpResponse = await _httpExecutor.ExecuteAsync(
            httpRequest,
            followRedirects: true,
            timeoutMs: 30000,
            cancellationToken);

        var replayWitnessId = _witnessIdGenerator.Generate(
            options.Tag,
            originalInteraction.Request.Method.Value,
            originalInteraction.Request.Path,
            originalInteraction.Request.Body);

        var metadata = InteractionMetadata.Create(tags: new List<string> { options.Tag, "replay" });

        var replayInteraction = Interaction.Create(
            replayWitnessId,
            originalSessionId,
            DateTimeOffset.UtcNow,
            httpRequest,
            httpResponse,
            metadata);

        await _interactionRepository.SaveAsync(replayInteraction, cancellationToken);

        var session = await _sessionRepository.GetByIdAsync(originalSessionId, cancellationToken);
        session?.AddInteraction(replayWitnessId.Value);
        if (session != null)
        {
            await _sessionRepository.SaveAsync(session, cancellationToken);
        }

        return new ReplayResponseDto
        {
            OriginalWitnessId = request.WitnessId,
            ReplayWitnessId = replayWitnessId.Value,
            StatusCode = httpResponse.StatusCode,
            DurationMs = httpResponse.DurationMs,
            ResponseBody = ParseJsonOrReturnString(httpResponse.Body),
            Stored = true
        };
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.TrimStart('/');
        return $"{trimmedBase}/{trimmedPath}";
    }

    private static object? ParseJsonOrReturnString(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            return JsonSerializer.Deserialize<object>(body);
        }
        catch
        {
            return body;
        }
    }
}

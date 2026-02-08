using Witness.Application.DTOs;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Domain.Services;
using Witness.Domain.ValueObjects;
using Witness.Application.Interfaces;
using System.Text.Json;

namespace Witness.Application.UseCases;

/// <summary>
/// Use case for recording HTTP interactions
/// </summary>
public sealed class RecordInteractionUseCase
{
    private readonly IHttpExecutor _httpExecutor;
    private readonly IWitnessIdGenerator _witnessIdGenerator;
    private readonly IInteractionRepository _interactionRepository;
    private readonly ISessionRepository _sessionRepository;

    public RecordInteractionUseCase(
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

    public async Task<RecordResponseDto> ExecuteAsync(RecordRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var options = request.Options ?? new RecordOptionsDto();

        var fullUrl = CombineUrl(request.Target, request.Path);
        var httpMethod = Domain.ValueObjects.HttpMethod.FromString(request.Method);

        var httpRequest = HttpRequest.Create(
            httpMethod,
            fullUrl,
            request.Path,
            request.Headers,
            request.Body,
            GetContentType(request.Headers));

        var httpResponse = await _httpExecutor.ExecuteAsync(
            httpRequest,
            options.FollowRedirects,
            options.TimeoutMs,
            cancellationToken);

        var witnessId = _witnessIdGenerator.Generate(
            options.Tag,
            request.Method,
            request.Path,
            request.Body);

        var metadata = InteractionMetadata.Create(tags: new List<string> { options.Tag });

        var interaction = Interaction.Create(
            witnessId,
            options.SessionId,
            DateTimeOffset.UtcNow,
            httpRequest,
            httpResponse,
            metadata);

        await EnsureSessionExistsAsync(options.SessionId, cancellationToken);
        await _interactionRepository.SaveAsync(interaction, cancellationToken);

        var session = await _sessionRepository.GetByIdAsync(options.SessionId, cancellationToken);
        session?.AddInteraction(witnessId.Value);
        if (session != null)
        {
            await _sessionRepository.SaveAsync(session, cancellationToken);
        }

        return new RecordResponseDto
        {
            WitnessId = witnessId.Value,
            SessionId = options.SessionId,
            StatusCode = httpResponse.StatusCode,
            DurationMs = httpResponse.DurationMs,
            ResponseBody = ParseJsonOrReturnString(httpResponse.Body),
            ResponseHeaders = httpResponse.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Stored = true
        };
    }

    private async Task EnsureSessionExistsAsync(string sessionId, CancellationToken cancellationToken)
    {
        var exists = await _sessionRepository.ExistsAsync(sessionId, cancellationToken);
        if (!exists)
        {
            var session = Session.Create(sessionId, DateTimeOffset.UtcNow);
            await _sessionRepository.SaveAsync(session, cancellationToken);
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        var trimmedPath = path.TrimStart('/');
        return $"{trimmedBase}/{trimmedPath}";
    }

    private static string? GetContentType(Dictionary<string, string>? headers)
    {
        if (headers == null) return null;

        foreach (var (key, value) in headers)
        {
            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }
        return null;
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

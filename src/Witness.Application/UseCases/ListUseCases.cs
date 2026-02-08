using Witness.Application.DTOs;
using Witness.Domain.Repositories;

namespace Witness.Application.UseCases;

/// <summary>
/// Use case for listing sessions and interactions
/// </summary>
public sealed class ListSessionsUseCase
{
    private readonly ISessionRepository _sessionRepository;

    public ListSessionsUseCase(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<ListSessionsResponseDto> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.ListAllAsync(cancellationToken);

        var sessionSummaries = sessions.Select(s => new SessionSummaryDto
        {
            SessionId = s.SessionId,
            CreatedAt = s.CreatedAt.ToString("o"),
            Description = s.Description,
            Tags = s.Tags.ToList(),
            InteractionCount = s.InteractionIds.Count
        }).ToList();

        return new ListSessionsResponseDto
        {
            Sessions = sessionSummaries,
            TotalCount = sessionSummaries.Count
        };
    }
}

/// <summary>
/// Use case for listing interactions in a session
/// </summary>
public sealed class ListInteractionsUseCase
{
    private readonly IInteractionRepository _interactionRepository;

    public ListInteractionsUseCase(IInteractionRepository interactionRepository)
    {
        _interactionRepository = interactionRepository ?? throw new ArgumentNullException(nameof(interactionRepository));
    }

    public async Task<ListInteractionsResponseDto> ExecuteAsync(ListInteractionsRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var interactions = await _interactionRepository.ListBySessionAsync(request.SessionId, cancellationToken);

        var interactionSummaries = interactions.Select(i => new InteractionSummaryDto
        {
            WitnessId = i.WitnessId.Value,
            Timestamp = i.Timestamp.ToString("o"),
            Method = i.Request.Method.Value,
            Path = i.Request.Path,
            StatusCode = i.Response.StatusCode,
            DurationMs = i.Response.DurationMs
        }).ToList();

        return new ListInteractionsResponseDto
        {
            SessionId = request.SessionId,
            Interactions = interactionSummaries,
            TotalCount = interactionSummaries.Count
        };
    }
}

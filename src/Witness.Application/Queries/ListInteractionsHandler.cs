using MediatR;
using Microsoft.Extensions.Logging;
using Witness.Domain.Repositories;

namespace Witness.Application.Queries;

public sealed class ListInteractionsHandler : IRequestHandler<ListInteractionsQuery, ListInteractionsResult>
{
    private readonly IInteractionRepository _interactionRepository;
    private readonly ILogger<ListInteractionsHandler> _logger;

    public ListInteractionsHandler(
        IInteractionRepository interactionRepository,
        ILogger<ListInteractionsHandler> logger)
    {
        _interactionRepository = interactionRepository ?? throw new ArgumentNullException(nameof(interactionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ListInteractionsResult> Handle(ListInteractionsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing interactions for session: {SessionId}, limit: {Limit}", 
            request.SessionId, request.Limit);

        var interactions = await _interactionRepository.ListBySessionAsync(request.SessionId, request.Limit, cancellationToken);

        var summaries = interactions.Select(i => new InteractionSummaryDto
        {
            WitnessId = i.Id.Value,
            Timestamp = i.Timestamp,
            Method = i.Request.Method,
            Path = i.Request.Path,
            StatusCode = i.Response.StatusCode,
            DurationMs = i.Response.DurationMs,
            Tags = new List<string>(i.Metadata.Tags)
        }).ToList();

        return new ListInteractionsResult
        {
            SessionId = request.SessionId,
            Count = summaries.Count,
            Total = summaries.Count,
            Interactions = summaries
        };
    }
}

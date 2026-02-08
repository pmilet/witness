using MediatR;
using Microsoft.Extensions.Logging;
using Witness.Application.DTOs;
using Witness.Domain.Repositories;

namespace Witness.Application.Queries;

public sealed class ListSessionsHandler : IRequestHandler<ListSessionsQuery, ListSessionsResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<ListSessionsHandler> _logger;

    public ListSessionsHandler(
        ISessionRepository sessionRepository,
        ILogger<ListSessionsHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ListSessionsResult> Handle(ListSessionsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing sessions with limit: {Limit}", request.Limit);

        var sessions = await _sessionRepository.ListAsync(request.Limit, cancellationToken);

        var sessionDtos = sessions.Select(s => new SessionDto
        {
            SessionId = s.SessionId,
            CreatedAt = s.CreatedAt,
            Tags = new List<string>(s.Tags),
            InteractionCount = s.InteractionCount,
            Description = s.Description
        }).ToList();

        return new ListSessionsResult
        {
            Count = sessionDtos.Count,
            Total = sessionDtos.Count,
            Sessions = sessionDtos
        };
    }
}

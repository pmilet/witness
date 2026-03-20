using MediatR;
using Microsoft.Extensions.Logging;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Domain.Services;
using Witness.Domain.ValueObjects;

namespace Witness.Application.Commands;

public sealed class ReplayInteractionHandler : IRequestHandler<ReplayInteractionCommand, ReplayInteractionResult>
{
    private readonly IHttpExecutor _httpExecutor;
    private readonly IInteractionRepository _interactionRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<ReplayInteractionHandler> _logger;

    public ReplayInteractionHandler(
        IHttpExecutor httpExecutor,
        IInteractionRepository interactionRepository,
        ISessionRepository sessionRepository,
        ILogger<ReplayInteractionHandler> logger)
    {
        _httpExecutor = httpExecutor ?? throw new ArgumentNullException(nameof(httpExecutor));
        _interactionRepository = interactionRepository ?? throw new ArgumentNullException(nameof(interactionRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReplayInteractionResult> Handle(ReplayInteractionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Replaying interaction: {WitnessId}", request.WitnessId);

        // Load original interaction
        var originalWitnessId = WitnessId.Parse(request.WitnessId);
        var original = await _interactionRepository.GetByIdAsync(originalWitnessId, request.Options?.SessionId, cancellationToken);

        if (original == null)
        {
            throw new InvalidOperationException($"Interaction not found: {request.WitnessId}");
        }

        // Prepare headers (merge original with overrides)
        var headers = new Dictionary<string, string>(original.Request.Headers);
        if (request.Options?.OverrideHeaders != null)
        {
            foreach (var kvp in request.Options.OverrideHeaders)
            {
                headers[kvp.Key] = kvp.Value;
            }
        }

        // Execute replay
        var executionResult = await _httpExecutor.ExecuteAsync(
            request.Target,
            original.Request.Method,
            original.Request.Path,
            headers.AsReadOnly(),
            original.Request.Body,
            cancellationToken: cancellationToken);

        // Generate new WitnessId for replay
        var tag = request.Options?.Tag ?? $"replay-{original.Metadata.Tags.FirstOrDefault() ?? "interaction"}";
        var replayWitnessId = WitnessId.Generate(tag, original.Request.Method, original.Request.Path, original.Request.Body);

        // Determine session ID
        var sessionId = request.Options?.SessionId ?? original.SessionId;

        // Create replay interaction
        var metadata = new InteractionMetadata(
            tags: new[] { tag },
            description: $"Replay of {request.WitnessId}");

        var replayInteraction = Interaction.Create(
            replayWitnessId,
            sessionId,
            executionResult.Request,
            executionResult.Response,
            metadata);

        // Save replay
        await _interactionRepository.SaveAsync(replayInteraction, cancellationToken);

        // Update session
        await UpdateSessionAsync(sessionId, replayInteraction, cancellationToken);

        _logger.LogInformation("Interaction replayed: {ReplayWitnessId}", replayWitnessId.Value);

        return new ReplayInteractionResult
        {
            OriginalWitnessId = request.WitnessId,
            ReplayWitnessId = replayWitnessId.Value,
            StatusCode = executionResult.Response.StatusCode,
            DurationMs = executionResult.DurationMs,
            ResponseBody = executionResult.Response.Body,
            Stored = true
        };
    }

    private async Task UpdateSessionAsync(string sessionId, Interaction interaction, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken);
        
        if (session == null)
        {
            session = Session.Create(sessionId);
        }

        session.IncrementInteractionCount();
        session.AddTags(interaction.Metadata.Tags);

        await _sessionRepository.SaveAsync(session, cancellationToken);
    }
}

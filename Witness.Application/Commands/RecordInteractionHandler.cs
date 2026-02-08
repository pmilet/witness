using MediatR;
using Microsoft.Extensions.Logging;
using Witness.Domain.Entities;
using Witness.Domain.Repositories;
using Witness.Domain.Services;
using Witness.Domain.ValueObjects;

namespace Witness.Application.Commands;

public sealed class RecordInteractionHandler : IRequestHandler<RecordInteractionCommand, RecordInteractionResult>
{
    private readonly IHttpExecutor _httpExecutor;
    private readonly IInteractionRepository _interactionRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<RecordInteractionHandler> _logger;

    public RecordInteractionHandler(
        IHttpExecutor httpExecutor,
        IInteractionRepository interactionRepository,
        ISessionRepository sessionRepository,
        ILogger<RecordInteractionHandler> logger)
    {
        _httpExecutor = httpExecutor ?? throw new ArgumentNullException(nameof(httpExecutor));
        _interactionRepository = interactionRepository ?? throw new ArgumentNullException(nameof(interactionRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RecordInteractionResult> Handle(RecordInteractionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recording interaction: {Method} {Target}{Path}", 
            request.Method, request.Target, request.Path);

        // Prepare execution options
        var options = new HttpExecutionOptions
        {
            TimeoutMs = request.Options?.TimeoutMs ?? 30000,
            FollowRedirects = request.Options?.FollowRedirects ?? true
        };

        // Execute HTTP request
        var executionResult = await _httpExecutor.ExecuteAsync(
            request.Target,
            request.Method,
            request.Path,
            request.Headers?.AsReadOnly(),
            request.Body,
            options,
            cancellationToken);

        // Generate WitnessId
        var tag = request.Options?.Tag ?? "interaction";
        var witnessId = WitnessId.Generate(tag, request.Method, request.Path, request.Body);

        // Determine session ID
        var sessionId = request.Options?.SessionId ?? GenerateDefaultSessionId();

        // Create interaction entity
        var metadata = new InteractionMetadata(
            tags: new[] { tag },
            description: request.Options?.Description);

        var interaction = Interaction.Create(
            witnessId,
            sessionId,
            executionResult.Request,
            executionResult.Response,
            metadata);

        // Save interaction
        await _interactionRepository.SaveAsync(interaction, cancellationToken);

        // Update session
        await UpdateSessionAsync(sessionId, interaction, cancellationToken);

        _logger.LogInformation("Interaction recorded: {WitnessId}", witnessId.Value);

        // Return result
        return new RecordInteractionResult
        {
            WitnessId = witnessId.Value,
            SessionId = sessionId,
            StatusCode = executionResult.Response.StatusCode,
            DurationMs = executionResult.DurationMs,
            ResponseBody = executionResult.Response.Body,
            ResponseHeaders = executionResult.Response.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
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

    private static string GenerateDefaultSessionId()
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return $"session-{date}";
    }
}

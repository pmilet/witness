using MediatR;
using Microsoft.Extensions.Logging;
using Witness.Application.DTOs;
using Witness.Domain.Repositories;
using Witness.Domain.ValueObjects;

namespace Witness.Application.Queries;

public sealed class InspectInteractionHandler : IRequestHandler<InspectInteractionQuery, InteractionDto?>
{
    private readonly IInteractionRepository _interactionRepository;
    private readonly ILogger<InspectInteractionHandler> _logger;

    public InspectInteractionHandler(
        IInteractionRepository interactionRepository,
        ILogger<InspectInteractionHandler> logger)
    {
        _interactionRepository = interactionRepository ?? throw new ArgumentNullException(nameof(interactionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InteractionDto?> Handle(InspectInteractionQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Inspecting interaction: {WitnessId}", request.WitnessId);

        var witnessId = WitnessId.Parse(request.WitnessId);
        var interaction = await _interactionRepository.GetByIdAsync(witnessId, request.SessionId, cancellationToken);

        if (interaction == null)
        {
            _logger.LogWarning("Interaction not found: {WitnessId}", request.WitnessId);
            return null;
        }

        return new InteractionDto
        {
            WitnessId = interaction.Id.Value,
            SessionId = interaction.SessionId,
            Timestamp = interaction.Timestamp,
            Request = new HttpRequestDto
            {
                Method = interaction.Request.Method,
                Url = interaction.Request.Url,
                Path = interaction.Request.Path,
                Headers = new Dictionary<string, string>(interaction.Request.Headers),
                Body = interaction.Request.Body,
                ContentType = interaction.Request.ContentType
            },
            Response = new HttpResponseDto
            {
                StatusCode = interaction.Response.StatusCode,
                Headers = new Dictionary<string, string>(interaction.Response.Headers),
                Body = interaction.Response.Body,
                ContentType = interaction.Response.ContentType,
                DurationMs = interaction.Response.DurationMs
            },
            Metadata = new InteractionMetadataDto
            {
                Tags = new List<string>(interaction.Metadata.Tags),
                Description = interaction.Metadata.Description,
                OpenApiOperationId = interaction.Metadata.OpenApiOperationId,
                ChainStep = interaction.Metadata.ChainStep,
                ChainId = interaction.Metadata.ChainId
            }
        };
    }
}

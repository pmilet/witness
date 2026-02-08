using Witness.Application.DTOs;
using Witness.Domain.Repositories;
using Witness.Domain.ValueObjects;

namespace Witness.Application.UseCases;

/// <summary>
/// Use case for inspecting a recorded interaction
/// </summary>
public sealed class InspectInteractionUseCase
{
    private readonly IInteractionRepository _interactionRepository;

    public InspectInteractionUseCase(IInteractionRepository interactionRepository)
    {
        _interactionRepository = interactionRepository ?? throw new ArgumentNullException(nameof(interactionRepository));
    }

    public async Task<InteractionDto> ExecuteAsync(InspectRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var witnessId = WitnessId.Parse(request.WitnessId);
        var interaction = await _interactionRepository.GetByIdAsync(witnessId, request.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Interaction with WitnessId {request.WitnessId} not found in session {request.SessionId}");

        return new InteractionDto
        {
            WitnessId = interaction.WitnessId.Value,
            SessionId = interaction.SessionId,
            Timestamp = interaction.Timestamp.ToString("o"),
            Request = new RequestDto
            {
                Method = interaction.Request.Method.Value,
                Url = interaction.Request.Url,
                Path = interaction.Request.Path,
                Headers = interaction.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Body = interaction.Request.Body,
                ContentType = interaction.Request.ContentType
            },
            Response = new ResponseDto
            {
                StatusCode = interaction.Response.StatusCode,
                Headers = interaction.Response.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Body = interaction.Response.Body,
                ContentType = interaction.Response.ContentType,
                DurationMs = interaction.Response.DurationMs
            },
            Metadata = new MetadataDto
            {
                Tags = interaction.Metadata.Tags.ToList(),
                Description = interaction.Metadata.Description,
                OpenApiOperationId = interaction.Metadata.OpenApiOperationId,
                ChainStep = interaction.Metadata.ChainStep,
                ChainId = interaction.Metadata.ChainId
            }
        };
    }
}

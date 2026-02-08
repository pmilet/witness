using System.Text.Json;
using Witness.Application.DTOs;
using Witness.Application.UseCases;

namespace Witness.McpServer.Handlers;

/// <summary>
/// Handler for witness/inspect tool
/// </summary>
public sealed class InspectToolHandler
{
    private readonly InspectInteractionUseCase _useCase;

    public InspectToolHandler(InspectInteractionUseCase useCase)
    {
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
    }

    public async Task<string> HandleAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentsJson, nameof(argumentsJson));

        try
        {
            var request = JsonSerializer.Deserialize<InspectRequestDto>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new ArgumentException("Failed to deserialize inspect request");

            var response = await _useCase.ExecuteAsync(request, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                data = response
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
        }
    }
}

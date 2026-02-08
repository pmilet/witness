using System.Text.Json;
using Witness.Application.DTOs;
using Witness.Application.UseCases;

namespace Witness.McpServer.Handlers;

/// <summary>
/// Handler for witness/replay tool
/// </summary>
public sealed class ReplayToolHandler
{
    private readonly ReplayInteractionUseCase _useCase;

    public ReplayToolHandler(ReplayInteractionUseCase useCase)
    {
        _useCase = useCase ?? throw new ArgumentNullException(nameof(useCase));
    }

    public async Task<string> HandleAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentsJson, nameof(argumentsJson));

        try
        {
            var request = JsonSerializer.Deserialize<ReplayRequestDto>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new ArgumentException("Failed to deserialize replay request");

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

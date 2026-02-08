using System.Text.Json;
using Witness.Application.DTOs;
using Witness.Application.UseCases;

namespace Witness.McpServer.Handlers;

/// <summary>
/// Handler for witness/list tool
/// </summary>
public sealed class ListToolHandler
{
    private readonly ListSessionsUseCase _listSessionsUseCase;
    private readonly ListInteractionsUseCase _listInteractionsUseCase;

    public ListToolHandler(
        ListSessionsUseCase listSessionsUseCase,
        ListInteractionsUseCase listInteractionsUseCase)
    {
        _listSessionsUseCase = listSessionsUseCase ?? throw new ArgumentNullException(nameof(listSessionsUseCase));
        _listInteractionsUseCase = listInteractionsUseCase ?? throw new ArgumentNullException(nameof(listInteractionsUseCase));
    }

    public async Task<string> HandleSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _listSessionsUseCase.ExecuteAsync(cancellationToken);

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

    public async Task<string> HandleInteractionsAsync(string argumentsJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argumentsJson, nameof(argumentsJson));

        try
        {
            var request = JsonSerializer.Deserialize<ListInteractionsRequestDto>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new ArgumentException("Failed to deserialize list interactions request");

            var response = await _listInteractionsUseCase.ExecuteAsync(request, cancellationToken);

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

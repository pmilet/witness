using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Repositories;

namespace Witness.Infrastructure.Tests;

public class FileSystemInteractionRepositoryTests : IDisposable
{
    private readonly string _tempPath;
    private readonly FileSystemInteractionRepository _repository;

    public FileSystemInteractionRepositoryTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        var options = Options.Create(new WitnessOptions
        {
            Storage = new StorageOptions { Type = "local", Path = _tempPath }
        });

        _repository = new FileSystemInteractionRepository(
            options,
            NullLogger<FileSystemInteractionRepository>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ValidInteraction_PersistsToFile()
    {
        // Arrange
        var interaction = CreateTestInteraction("session-1");

        // Act
        await _repository.SaveAsync(interaction);

        // Assert
        var loaded = await _repository.GetByIdAsync(interaction.Id, "session-1");
        Assert.NotNull(loaded);
        Assert.Equal(interaction.Id.Value, loaded.Id.Value);
        Assert.Equal(interaction.SessionId, loaded.SessionId);
        Assert.Equal(interaction.Request.Method, loaded.Request.Method);
        Assert.Equal(interaction.Request.Path, loaded.Request.Path);
        Assert.Equal(interaction.Response.StatusCode, loaded.Response.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_WithSessionId_ReturnsInteraction()
    {
        // Arrange
        var interaction = CreateTestInteraction("session-abc");
        await _repository.SaveAsync(interaction);

        // Act
        var result = await _repository.GetByIdAsync(interaction.Id, "session-abc");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(interaction.Id.Value, result.Id.Value);
    }

    [Fact]
    public async Task GetByIdAsync_WithoutSessionId_SearchesAllSessions()
    {
        // Arrange
        var interaction = CreateTestInteraction("session-xyz");
        await _repository.SaveAsync(interaction);

        // Act - search without providing sessionId
        var result = await _repository.GetByIdAsync(interaction.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(interaction.Id.Value, result.Id.Value);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = WitnessId.Generate("test", "GET", "/api/missing", null);

        // Act
        var result = await _repository.GetByIdAsync(nonExistentId, "session-1");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListBySessionAsync_ReturnsAllInteractionsInSession()
    {
        // Arrange
        var sessionId = "session-list-test";
        var interaction1 = CreateTestInteraction(sessionId, "/api/users");
        var interaction2 = CreateTestInteraction(sessionId, "/api/orders");

        await _repository.SaveAsync(interaction1);
        await _repository.SaveAsync(interaction2);

        // Act
        var results = await _repository.ListBySessionAsync(sessionId);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ListBySessionAsync_EmptySession_ReturnsEmpty()
    {
        // Act
        var results = await _repository.ListBySessionAsync("non-existent-session");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ListBySessionAsync_RespectsLimit()
    {
        // Arrange
        var sessionId = "session-limit-test";
        for (int i = 0; i < 5; i++)
        {
            await _repository.SaveAsync(CreateTestInteraction(sessionId, $"/api/items/{i}"));
        }

        // Act
        var results = await _repository.ListBySessionAsync(sessionId, limit: 3);

        // Assert
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task SaveAsync_WithMetadata_PreservesMetadata()
    {
        // Arrange
        var interaction = CreateTestInteraction("session-meta", "/api/test");
        var metadata = new InteractionMetadata(
            tags: ["tag1", "tag2"],
            description: "Test description",
            openApiOperationId: "getTest",
            chainStep: 1,
            chainId: "chain-123");

        var withMetadata = Interaction.Create(
            interaction.Id,
            interaction.SessionId,
            interaction.Request,
            interaction.Response,
            metadata);

        // Act
        await _repository.SaveAsync(withMetadata);
        var loaded = await _repository.GetByIdAsync(withMetadata.Id, "session-meta");

        // Assert
        Assert.NotNull(loaded);
        Assert.Contains("tag1", loaded.Metadata.Tags);
        Assert.Contains("tag2", loaded.Metadata.Tags);
        Assert.Equal("Test description", loaded.Metadata.Description);
        Assert.Equal("getTest", loaded.Metadata.OpenApiOperationId);
        Assert.Equal(1, loaded.Metadata.ChainStep);
        Assert.Equal("chain-123", loaded.Metadata.ChainId);
    }

    private static Interaction CreateTestInteraction(string sessionId, string path = "/api/test")
    {
        var witnessId = WitnessId.Generate("test", "GET", path, null);
        var request = new HttpRequest("GET", $"https://example.com{path}", path);
        var response = new HttpResponse(200, durationMs: 100);

        return Interaction.Create(witnessId, sessionId, request, response);
    }
}

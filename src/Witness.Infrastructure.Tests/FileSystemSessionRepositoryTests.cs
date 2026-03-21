using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Witness.Domain.Entities;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Repositories;

namespace Witness.Infrastructure.Tests;

public class FileSystemSessionRepositoryTests : IDisposable
{
    private readonly string _tempPath;
    private readonly FileSystemSessionRepository _repository;

    public FileSystemSessionRepositoryTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        var options = Options.Create(new WitnessOptions
        {
            Storage = new StorageOptions { Type = "local", Path = _tempPath }
        });

        _repository = new FileSystemSessionRepository(
            options,
            NullLogger<FileSystemSessionRepository>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ValidSession_PersistsToFile()
    {
        // Arrange
        var session = Session.Create("session-save-test");

        // Act
        await _repository.SaveAsync(session);

        // Assert
        var loaded = await _repository.GetByIdAsync("session-save-test");
        Assert.NotNull(loaded);
        Assert.Equal("session-save-test", loaded.SessionId);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        var session = Session.Create("session-get-test");
        session.AddTag("tag1");
        await _repository.SaveAsync(session);

        // Act
        var result = await _repository.GetByIdAsync("session-get-test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("session-get-test", result.SessionId);
        Assert.Contains("tag1", result.Tags);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentSession_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existent-session");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListAsync_MultipleSessions_ReturnsAllSessions()
    {
        // Arrange
        var session1 = Session.Create("list-session-1");
        var session2 = Session.Create("list-session-2");
        await _repository.SaveAsync(session1);
        await Task.Delay(10); // Ensure different timestamps for ordering
        await _repository.SaveAsync(session2);

        // Act
        var results = await _repository.ListAsync();

        // Assert
        Assert.True(results.Count >= 2);
        Assert.Contains(results, s => s.SessionId == "list-session-1");
        Assert.Contains(results, s => s.SessionId == "list-session-2");
    }

    [Fact]
    public async Task ListAsync_EmptyStore_ReturnsEmpty()
    {
        // Act
        var results = await _repository.ListAsync();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SaveAsync_UpdatesInteractionCount_AfterIncrement()
    {
        // Arrange
        var session = Session.Create("session-count-test");
        session.IncrementInteractionCount();
        session.IncrementInteractionCount();

        // Act
        await _repository.SaveAsync(session);
        var loaded = await _repository.GetByIdAsync("session-count-test");

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.InteractionCount);
    }
}

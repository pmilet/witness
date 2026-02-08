using Moq;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Witness.Application.Commands;
using Witness.Domain.Repositories;
using Witness.Domain.Services;
using Witness.Domain.ValueObjects;
using Witness.Domain.Entities;
using Xunit;

namespace Witness.Application.Tests;

public class RecordInteractionHandlerTests
{
    private readonly Mock<IHttpExecutor> _mockHttpExecutor;
    private readonly Mock<IInteractionRepository> _mockInteractionRepository;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<ILogger<RecordInteractionHandler>> _mockLogger;
    private readonly RecordInteractionHandler _handler;

    public RecordInteractionHandlerTests()
    {
        _mockHttpExecutor = new Mock<IHttpExecutor>();
        _mockInteractionRepository = new Mock<IInteractionRepository>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockLogger = new Mock<ILogger<RecordInteractionHandler>>();
        
        _handler = new RecordInteractionHandler(
            _mockHttpExecutor.Object,
            _mockInteractionRepository.Object,
            _mockSessionRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_RecordsInteraction()
    {
        // Arrange
        var command = new RecordInteractionCommand
        {
            Target = "https://api.example.com",
            Method = "GET",
            Path = "/api/test",
            Options = new RecordOptions { Tag = "test-tag" }
        };

        var mockRequest = new HttpRequest("GET", "https://api.example.com/api/test", "/api/test");
        var mockResponse = new HttpResponse(200, durationMs: 100);
        var mockResult = new HttpExecutionResult(mockRequest, mockResponse, 100);

        _mockHttpExecutor
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<object>(),
                It.IsAny<HttpExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);

        _mockSessionRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.StatusCode.Should().Be(200);
        result.DurationMs.Should().Be(100);
        result.Stored.Should().BeTrue();
        result.WitnessId.Should().NotBeNullOrEmpty();

        _mockInteractionRepository.Verify(
            x => x.SaveAsync(It.IsAny<Interaction>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSessionRepository.Verify(
            x => x.SaveAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCustomSessionId_UsesProvidedSessionId()
    {
        // Arrange
        var customSessionId = "custom-session-123";
        var command = new RecordInteractionCommand
        {
            Target = "https://api.example.com",
            Method = "POST",
            Path = "/api/test",
            Options = new RecordOptions { SessionId = customSessionId }
        };

        var mockRequest = new HttpRequest("POST", "https://api.example.com/api/test", "/api/test");
        var mockResponse = new HttpResponse(201, durationMs: 150);
        var mockResult = new HttpExecutionResult(mockRequest, mockResponse, 150);

        _mockHttpExecutor
            .Setup(x => x.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<object>(),
                It.IsAny<HttpExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.SessionId.Should().Be(customSessionId);
    }
}

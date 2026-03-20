using Witness.Domain.Entities;
using Witness.Domain.ValueObjects;
using Xunit;

namespace Witness.Domain.Tests;

public class InteractionTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesInteraction()
    {
        // Arrange
        var id = WitnessId.Generate("test", "GET", "/api/test");
        var sessionId = "session-001";
        var request = new HttpRequest("GET", "https://api.example.com", "/api/test");
        var response = new HttpResponse(200);

        // Act
        var interaction = Interaction.Create(id, sessionId, request, response);

        // Assert
        Assert.NotNull(interaction);
        Assert.Equal(id, interaction.Id);
        Assert.Equal(sessionId, interaction.SessionId);
        Assert.Equal(request, interaction.Request);
        Assert.Equal(response, interaction.Response);
        Assert.NotNull(interaction.Metadata);
    }

    [Fact]
    public void Create_WithNullSessionId_ThrowsArgumentException()
    {
        // Arrange
        var id = WitnessId.Generate("test", "GET", "/api/test");
        var request = new HttpRequest("GET", "https://api.example.com", "/api/test");
        var response = new HttpResponse(200);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            Interaction.Create(id, null!, request, response));
    }

    [Fact]
    public void Recreate_WithTimestamp_PreservesTimestamp()
    {
        // Arrange
        var id = WitnessId.Generate("test", "GET", "/api/test");
        var sessionId = "session-001";
        var timestamp = new DateTime(2026, 2, 8, 10, 30, 0, DateTimeKind.Utc);
        var request = new HttpRequest("GET", "https://api.example.com", "/api/test");
        var response = new HttpResponse(200);
        var metadata = new InteractionMetadata(new[] { "test" });

        // Act
        var interaction = Interaction.Recreate(id, sessionId, timestamp, request, response, metadata);

        // Assert
        Assert.Equal(timestamp, interaction.Timestamp);
    }
}

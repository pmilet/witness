using Witness.Domain.Entities;
using Xunit;

namespace Witness.Domain.Tests;

public class SessionTests
{
    [Fact]
    public void Create_WithValidSessionId_CreatesSession()
    {
        // Arrange
        var sessionId = "session-001";

        // Act
        var session = Session.Create(sessionId);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(sessionId, session.SessionId);
        Assert.NotNull(session.Tags);
        Assert.Empty(session.Tags);
        Assert.Equal(0, session.InteractionCount);
    }

    [Fact]
    public void AddTag_NewTag_AddsTag()
    {
        // Arrange
        var session = Session.Create("session-001");

        // Act
        session.AddTag("integration-test");

        // Assert
        Assert.Contains("integration-test", session.Tags);
    }

    [Fact]
    public void AddTag_DuplicateTag_DoesNotAddDuplicate()
    {
        // Arrange
        var session = Session.Create("session-001");
        session.AddTag("test");

        // Act
        session.AddTag("test");

        // Assert
        Assert.Single(session.Tags);
    }

    [Fact]
    public void IncrementInteractionCount_Increments()
    {
        // Arrange
        var session = Session.Create("session-001");
        var initialCount = session.InteractionCount;

        // Act
        session.IncrementInteractionCount();

        // Assert
        Assert.Equal(initialCount + 1, session.InteractionCount);
    }

    [Fact]
    public void AddTags_MultipleTags_AddsAll()
    {
        // Arrange
        var session = Session.Create("session-001");
        var tags = new[] { "tag1", "tag2", "tag3" };

        // Act
        session.AddTags(tags);

        // Assert
        Assert.Equal(3, session.Tags.Count);
        Assert.All(tags, tag => Assert.Contains(tag, session.Tags));
    }
}

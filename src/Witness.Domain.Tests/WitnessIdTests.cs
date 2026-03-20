using Witness.Domain.ValueObjects;
using Xunit;

namespace Witness.Domain.Tests;

public class WitnessIdTests
{
    [Fact]
    public void Generate_WithValidParameters_CreatesWitnessId()
    {
        // Arrange
        var tag = "test";
        var method = "POST";
        var path = "/api/loans";
        var body = new { amount = 100000 };

        // Act
        var witnessId = WitnessId.Generate(tag, method, path, body);

        // Assert
        Assert.NotNull(witnessId);
        Assert.Equal("test", witnessId.Tag);
        Assert.Equal("POST", witnessId.Method);
        Assert.NotEmpty(witnessId.PathSlug);
        Assert.NotEmpty(witnessId.BodyHash);
        Assert.NotEmpty(witnessId.Timestamp);
    }

    [Fact]
    public void Generate_WithNoBody_UsesZeroHash()
    {
        // Arrange
        var tag = "test";
        var method = "GET";
        var path = "/api/users";

        // Act
        var witnessId = WitnessId.Generate(tag, method, path, null);

        // Assert
        Assert.Equal("00000000", witnessId.BodyHash);
    }

    [Fact]
    public void Generate_WithComplexPath_CreatesSlug()
    {
        // Arrange
        var tag = "test";
        var method = "GET";
        var path = "/api/v1/users/123/orders";

        // Act
        var witnessId = WitnessId.Generate(tag, method, path);

        // Assert
        Assert.Equal("api-v1-users-123-orders", witnessId.PathSlug);
    }

    [Fact]
    public void Generate_WithQueryParameters_RemovesQuery()
    {
        // Arrange
        var tag = "test";
        var method = "GET";
        var path = "/api/users?limit=10&offset=20";

        // Act
        var witnessId = WitnessId.Generate(tag, method, path);

        // Assert
        Assert.Equal("api-users", witnessId.PathSlug);
    }

    [Fact]
    public void Generate_SameInputs_ProducesSameHash()
    {
        // Arrange
        var tag = "test";
        var method = "POST";
        var path = "/api/loans";
        var body = new { amount = 100000 };

        // Act
        var witnessId1 = WitnessId.Generate(tag, method, path, body);
        var witnessId2 = WitnessId.Generate(tag, method, path, body);

        // Assert - Same body hash (deterministic)
        Assert.Equal(witnessId1.BodyHash, witnessId2.BodyHash);
        // But different timestamps (unless run at exact same minute)
    }

    [Fact]
    public void Parse_ValidWitnessId_ReconstructsComponents()
    {
        // Arrange
        var original = WitnessId.Generate("mortgage", "POST", "/api/loans", new { amount = 250000 });

        // Act
        var parsed = WitnessId.Parse(original.Value);

        // Assert
        Assert.Equal(original.Tag, parsed.Tag);
        Assert.Equal(original.Method, parsed.Method);
        Assert.Equal(original.PathSlug, parsed.PathSlug);
        Assert.Equal(original.BodyHash, parsed.BodyHash);
        Assert.Equal(original.Timestamp, parsed.Timestamp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_NullOrEmptyTag_ThrowsArgumentException(string? tag)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            WitnessId.Generate(tag!, "GET", "/api/test"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_NullOrEmptyMethod_ThrowsArgumentException(string? method)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            WitnessId.Generate("test", method!, "/api/test"));
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        // Arrange
        var id1 = WitnessId.Generate("test", "GET", "/api/test");
        var id2 = WitnessId.Parse(id1.Value);

        // Act & Assert
        Assert.True(id1.Equals(id2));
        Assert.True(id1 == id2);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var witnessId = WitnessId.Generate("test", "GET", "/api/test");

        // Act
        var stringValue = witnessId.ToString();

        // Assert
        Assert.Equal(witnessId.Value, stringValue);
    }
}

namespace Witness.Infrastructure.Persistence;

public sealed class SessionModel
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    public int InteractionCount { get; set; }
    public string? Description { get; set; }
}

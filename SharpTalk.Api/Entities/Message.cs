namespace SharpTalk.Api.Entities;

public class Message
{
    public int Id { get; set; }
    public int ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsEdited { get; set; }
}

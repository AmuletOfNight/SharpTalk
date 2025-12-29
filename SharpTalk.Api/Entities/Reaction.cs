namespace SharpTalk.Api.Entities;

public class Reaction
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string EmojiCode { get; set; } = string.Empty; // e.g., :smile:, ❤️
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

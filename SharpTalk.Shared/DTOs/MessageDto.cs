namespace SharpTalk.Shared.DTOs;

public class MessageDto
{
    public int Id { get; set; }
    public int ChannelId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string UserStatus { get; set; } = "Offline"; // Online, Offline, Away
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<AttachmentDto> Attachments { get; set; } = new List<AttachmentDto>();
}

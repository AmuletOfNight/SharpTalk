namespace SharpTalk.Api.Entities;

public class ChannelMember
{
    public int ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

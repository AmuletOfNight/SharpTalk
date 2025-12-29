namespace SharpTalk.Api.Entities;

public class Channel
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPrivate { get; set; }
    public ICollection<ChannelMember> Members { get; set; } = new List<ChannelMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

namespace SharpTalk.Api.Entities;

public class Workspace
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<Channel> Channels { get; set; } = new List<Channel>();
}

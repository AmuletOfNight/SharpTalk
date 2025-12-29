namespace SharpTalk.Api.Entities;

public class WorkspaceMember
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "Member";
    public int OrderIndex { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

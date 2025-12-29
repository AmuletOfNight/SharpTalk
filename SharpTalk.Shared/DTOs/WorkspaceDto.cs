namespace SharpTalk.Shared.DTOs;

public class WorkspaceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

namespace SharpTalk.Shared.DTOs;

public class WorkspaceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public int MemberCount { get; set; }
}

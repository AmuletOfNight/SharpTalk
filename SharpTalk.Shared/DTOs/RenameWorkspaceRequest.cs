namespace SharpTalk.Shared.DTOs;

public class RenameWorkspaceRequest
{
    public int WorkspaceId { get; set; }
    public string NewName { get; set; } = string.Empty;
}

namespace SharpTalk.Shared.DTOs;

public class UpdateWorkspaceDescriptionRequest
{
    public int WorkspaceId { get; set; }
    public string Description { get; set; } = string.Empty;
}

namespace SharpTalk.Shared.DTOs;

public class CreateChannelRequest
{
    public int WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
}

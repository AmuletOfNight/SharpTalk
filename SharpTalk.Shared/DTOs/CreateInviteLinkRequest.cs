namespace SharpTalk.Shared.DTOs;

public class CreateInviteLinkRequest
{
    public int WorkspaceId { get; set; }
    public int? MaxUses { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

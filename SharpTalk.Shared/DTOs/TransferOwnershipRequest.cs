namespace SharpTalk.Shared.DTOs;

public class TransferOwnershipRequest
{
    public int WorkspaceId { get; set; }
    public int NewOwnerId { get; set; }
}

namespace SharpTalk.Shared.DTOs;

public class UpdateMemberRoleRequest
{
    public int WorkspaceId { get; set; }
    public int UserId { get; set; }
    public string NewRole { get; set; } = "Member";
}

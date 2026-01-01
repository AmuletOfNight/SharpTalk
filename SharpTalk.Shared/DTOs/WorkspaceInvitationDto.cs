using SharpTalk.Shared.Enums;

namespace SharpTalk.Shared.DTOs;

public class WorkspaceInvitationDto
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;
    public int InviterId { get; set; }
    public string InviterUsername { get; set; } = string.Empty;
    public int? InviteeId { get; set; }
    public string? InviteeUsername { get; set; }
    public InvitationType Type { get; set; }
    public InvitationStatus Status { get; set; }
    public string? Code { get; set; }
    public int? MaxUses { get; set; }
    public int UseCount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

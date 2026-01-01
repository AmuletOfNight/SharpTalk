using System.ComponentModel.DataAnnotations;
using SharpTalk.Shared.Enums;

namespace SharpTalk.Api.Entities;

public class WorkspaceInvitation
{
    public int Id { get; set; }

    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public int InviterId { get; set; }
    public User Inviter { get; set; } = null!;

    // Nullable for Link invitations
    public int? InviteeId { get; set; }
    public User? Invitee { get; set; }

    // For Link invitations
    [MaxLength(50)]
    public string? Code { get; set; }

    public InvitationType Type { get; set; }
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public int? MaxUses { get; set; }
    public int UseCount { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

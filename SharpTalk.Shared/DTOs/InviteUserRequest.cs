using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class InviteUserRequest
{
    public int WorkspaceId { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;
}

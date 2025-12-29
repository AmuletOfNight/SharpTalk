using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class CreateDirectMessageRequest
{
    public int? WorkspaceId { get; set; }

    [Required]
    public int TargetUserId { get; set; }
}

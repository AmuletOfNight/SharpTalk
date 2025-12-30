using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class CreateGroupDMRequest
{
    public int? WorkspaceId { get; set; }

    [Required]
    [MinLength(2, ErrorMessage = "Group must have at least 2 additional members")]
    [MaxLength(8, ErrorMessage = "Group can have at most 8 additional members")]
    public List<int> MemberIds { get; set; } = new();

    [MaxLength(100, ErrorMessage = "Group name cannot exceed 100 characters")]
    public string? Name { get; set; }
}

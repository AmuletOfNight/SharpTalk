using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class UpdateGroupNameRequest
{
    [Required]
    public int ChannelId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Group name cannot be empty")]
    [MaxLength(100, ErrorMessage = "Group name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;
}

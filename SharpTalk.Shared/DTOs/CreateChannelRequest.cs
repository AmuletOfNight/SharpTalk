using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class CreateChannelRequest
{
    [Required(ErrorMessage = "Workspace ID is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Invalid workspace ID")]
    public int WorkspaceId { get; set; }
    
    [Required(ErrorMessage = "Channel name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Channel name must be between 1 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\s]+$", ErrorMessage = "Channel name can only contain letters, numbers, spaces, hyphens, and underscores.")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; } = string.Empty;
    
    public bool IsPrivate { get; set; }
}

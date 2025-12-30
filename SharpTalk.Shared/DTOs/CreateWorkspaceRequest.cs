using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class CreateWorkspaceRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Workspace name must be between 1 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\s]+$", ErrorMessage = "Workspace name can only contain letters, numbers, spaces, hyphens, and underscores.")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }
}

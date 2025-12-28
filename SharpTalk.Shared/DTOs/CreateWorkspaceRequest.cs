using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class CreateWorkspaceRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Workspace name must be between 3 and 50 characters.")]
    public string Name { get; set; } = string.Empty;
}

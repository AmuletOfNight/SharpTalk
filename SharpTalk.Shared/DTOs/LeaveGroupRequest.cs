using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

/// <summary>
/// Request DTO for leaving a group DM
/// </summary>
public class LeaveGroupRequest
{
    [Required]
    public int ChannelId { get; set; }
}
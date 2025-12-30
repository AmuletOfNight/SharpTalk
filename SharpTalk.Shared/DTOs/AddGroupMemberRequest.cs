using System.ComponentModel.DataAnnotations;

namespace SharpTalk.Shared.DTOs;

public class AddGroupMemberRequest
{
    [Required]
    public int ChannelId { get; set; }

    [Required]
    public int UserId { get; set; }
}

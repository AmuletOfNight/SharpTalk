using SharpTalk.Shared.Enums;

namespace SharpTalk.Shared.DTOs;

public class ChannelDto
{
    public int Id { get; set; }
    public int? WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public string? AvatarUrl { get; set; }
    public ChannelType Type { get; set; }
    public bool CanMessage { get; set; } = true;
    public string UserStatus { get; set; } = "Offline"; // For DM channels: Online, Offline, Away
    public int? TargetUserId { get; set; } // For DM channels: the other user's ID
    
    // Group DM properties
    public List<GroupMemberDto>? Members { get; set; }
    public int MemberCount { get; set; }
    public bool IsGroup => Type == ChannelType.Group;
}

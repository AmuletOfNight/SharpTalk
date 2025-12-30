namespace SharpTalk.Shared.DTOs;

public class GroupMemberDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Status { get; set; } = "Offline";
    public DateTime JoinedAt { get; set; }
}

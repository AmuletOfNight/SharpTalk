namespace SharpTalk.Shared.DTOs;

public class WorkspaceMemberDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; }
    public bool IsOnline { get; set; }
    public bool IsCurrentUser { get; set; }
}

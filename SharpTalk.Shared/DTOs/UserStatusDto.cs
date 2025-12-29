namespace SharpTalk.Shared.DTOs;

public class UserStatusDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = "Offline"; // Online, Offline, Away
}

namespace SharpTalk.Shared.DTOs;

public class SendMessageRequest
{
    public int ChannelId { get; set; }
    public string Content { get; set; } = string.Empty;
}

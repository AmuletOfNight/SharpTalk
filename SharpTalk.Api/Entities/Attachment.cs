namespace SharpTalk.Api.Entities;

public class Attachment
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public Message Message { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // e.g., image/png, application/pdf
    public long FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

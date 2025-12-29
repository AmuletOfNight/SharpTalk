namespace SharpTalk.Shared;

public static class FileUploadConstants
{
    public static readonly string[] AllowedMimeTypes = new[]
    {
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml",
        "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/zip", "application/x-rar-compressed", "application/x-7z-compressed",
        "application/x-tar", "application/gzip",
        "text/plain", "text/csv", "application/json", "application/xml", "text/xml"
    };

    public const long MaxImageFileSize = 5 * 1024 * 1024; // 5MB
    public const long MaxOtherFileSize = 10 * 1024 * 1024; // 10MB
}

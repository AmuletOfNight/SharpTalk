using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;

namespace SharpTalk.Api.Services;

public class FileUploadService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration configuration, ILogger<FileUploadService> logger)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<AttachmentDto>> UploadFilesAsync(List<Microsoft.AspNetCore.Http.IFormFile> files, int messageId, int userId)
    {
        var attachments = new List<Attachment>();

        foreach (var file in files)
        {
            if (file == null || file.Length == 0) continue;

            // Validate file type
            var allowedTypes = new[] { 
                "image/jpeg", "image/png", "image/gif", "image/webp", "image/svg+xml",
                "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "application/zip", "application/x-rar-compressed", "application/x-7z-compressed",
                "application/x-tar", "application/gzip",
                "text/plain", "text/csv", "application/json", "application/xml", "text/xml"
            };

            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                throw new ArgumentException($"Invalid file type: {file.ContentType}");
            }

            // Validate file size
            var isImage = file.ContentType.StartsWith("image/");
            var maxImageFileSize = int.Parse(_configuration["FileUploadSettings:MaxImageFileSizeBytes"] ?? "5242880");
            var maxOtherFileSize = int.Parse(_configuration["FileUploadSettings:MaxOtherFileSizeBytes"] ?? "10485760");
            var maxSize = isImage ? maxImageFileSize : maxOtherFileSize;

            if (file.Length > maxSize)
            {
                throw new ArgumentException($"File size exceeds maximum allowed size of {maxSize / (1024 * 1024)}MB");
            }

            // Create uploads directory if it doesn't exist
            var uploadsPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "files");
            _logger.LogInformation("Uploads path: {UploadsPath}, Exists={Exists}", uploadsPath, Directory.Exists(uploadsPath));
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
                _logger.LogInformation("Created uploads directory: {UploadsPath}", uploadsPath);
            }

            // Generate unique filename
            var extension = Path.GetExtension(file.FileName).ToLower();
            var randomFilename = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsPath, randomFilename);
            _logger.LogInformation("Saving file: OriginalName={OriginalName}, RandomFilename={RandomFilename}, FilePath={FilePath}", file.FileName, randomFilename, filePath);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            _logger.LogInformation("File saved successfully: Size={Size}", file.Length);

            // Create attachment record
            var fileUrl = $"/uploads/files/{randomFilename}";
            var attachment = new Attachment
            {
                MessageId = messageId,
                FileName = file.FileName,
                FileUrl = fileUrl,
                FileType = file.ContentType,
                FileSize = file.Length,
                CreatedAt = DateTime.UtcNow
            };
            _logger.LogInformation("Created attachment record: FileUrl={FileUrl}, FileName={FileName}", fileUrl, file.FileName);

            _context.Attachments.Add(attachment);
            attachments.Add(attachment);
        }

        await _context.SaveChangesAsync();

        // Return DTOs
        return attachments.Select(a => new AttachmentDto
        {
            Id = a.Id,
            MessageId = a.MessageId,
            FileName = a.FileName,
            FileUrl = a.FileUrl,
            FileType = a.FileType,
            FileSize = a.FileSize,
            CreatedAt = a.CreatedAt
        }).ToList();
    }

    public async Task<AttachmentDto?> GetAttachmentAsync(int attachmentId)
    {
        var attachment = await _context.Attachments.FindAsync(attachmentId);
        if (attachment == null) return null;

        return new AttachmentDto
        {
            Id = attachment.Id,
            MessageId = attachment.MessageId,
            FileName = attachment.FileName,
            FileUrl = attachment.FileUrl,
            FileType = attachment.FileType,
            FileSize = attachment.FileSize,
            CreatedAt = attachment.CreatedAt
        };
    }

    public async Task<bool> CanAccessAttachmentAsync(int attachmentId, int userId)
    {
        var attachment = await _context.Attachments
            .Include(a => a.Message)
            .ThenInclude(m => m.Channel)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null) return false;

        var channel = attachment.Message.Channel;

        // Check if user is in the workspace
        var inWorkspace = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);

        if (!inWorkspace) return false;

        // If private channel, check if user is in channel
        if (channel.IsPrivate)
        {
            return await _context.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channel.Id && cm.UserId == userId);
        }

        return true;
    }
}

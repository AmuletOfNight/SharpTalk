using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared;
using SharpTalk.Shared.DTOs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

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
            if (!FileUploadConstants.AllowedMimeTypes.Contains(file.ContentType.ToLower()))
            {
                throw new ArgumentException($"Invalid file type: {file.ContentType}");
            }

            // Validate file size
            var isImage = file.ContentType.StartsWith("image/");
            var maxSize = isImage ? FileUploadConstants.MaxImageFileSize : FileUploadConstants.MaxOtherFileSize;

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

    public async Task<string> ProcessAvatarAsync(Microsoft.AspNetCore.Http.IFormFile file, string sanitizedUsername)
    {
        if (file == null || file.Length == 0) throw new ArgumentException("No file uploaded");

        // Create user-specific directory
        var userAvatarPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "avatars", sanitizedUsername);
        if (!Directory.Exists(userAvatarPath))
        {
            Directory.CreateDirectory(userAvatarPath);
        }

        var baseFilename = Guid.NewGuid().ToString();
        var resultUrl = string.Empty;

        // Define sizes: Small (32), Medium (64), Large (128)
        var sizes = new Dictionary<string, int>
        {
            { "s", 32 },
            { "m", 64 },
            { "l", 128 }
        };

        using var image = await Image.LoadAsync(file.OpenReadStream());

        foreach (var size in sizes)
        {
            using var resized = image.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(size.Value, size.Value),
                Mode = ResizeMode.Crop
            }));

            var filename = $"{baseFilename}_{size.Key}.webp";
            var filePath = Path.Combine(userAvatarPath, filename);

            await resized.SaveAsync(filePath, new WebpEncoder { Quality = 80 });

            if (size.Key == "m") // Use medium as the "default" URL stored in DB
            {
                resultUrl = $"/uploads/avatars/{sanitizedUsername}/{filename}";
            }
        }

        // Also save a original-ish version but optimized
        var originalFilename = $"{baseFilename}.webp";
        var originalPath = Path.Combine(userAvatarPath, originalFilename);
        await image.SaveAsync(originalPath, new WebpEncoder { Quality = 90 });

        return resultUrl;
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

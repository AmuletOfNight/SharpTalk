using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Api.Services;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;
using System.Security.Claims;

namespace SharpTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessageController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly FileUploadService _fileUploadService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MessageController> _logger;
    private readonly IDatabase _redis;

    public MessageController(ApplicationDbContext context, FileUploadService fileUploadService, IConfiguration configuration, ILogger<MessageController> logger, IConnectionMultiplexer redis)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _configuration = configuration;
        _logger = logger;
        _redis = redis.GetDatabase();
    }

    [HttpGet("{channelId}")]
    public async Task<ActionResult<List<MessageDto>>> GetMessages(int channelId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // [SECURITY] Verify user is member of the workspace for this channel
        var channel = await _context.Channels.FindAsync(channelId);
        if (channel == null)
        {
            return NotFound("Channel not found.");
        }

        if (channel.IsPrivate)
        {
            var isChannelMember = await _context.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId);
            if (!isChannelMember) return Forbid();
        }
        else
        {
            var isMember = await _context.WorkspaceMembers
                .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);

            if (!isMember)
            {
                return Forbid();
            }
        }

        var maxMessagesToRetrieve = int.Parse(_configuration["MessageSettings:MaxMessagesToRetrieve"] ?? "50");

        var messages = await _context.Messages
            .Where(m => m.ChannelId == channelId)
            .Include(m => m.User) // Include user to get username
            .Include(m => m.Attachments) // Include attachments
            .OrderBy(m => m.Timestamp)
            .Take(maxMessagesToRetrieve)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ChannelId = m.ChannelId,
                UserId = m.UserId,
                Username = m.User.Username,
                AvatarUrl = m.User.AvatarUrl,
                UserStatus = m.User.Status,
                Content = m.Content,
                Timestamp = m.Timestamp,
                Attachments = m.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id,
                    MessageId = a.MessageId,
                    FileName = a.FileName,
                    FileUrl = a.FileUrl,
                    FileType = a.FileType,
                    FileSize = a.FileSize,
                    CreatedAt = a.CreatedAt
                }).ToList()
            })
            .ToListAsync();

        var onlineUsers = await _redis.SetMembersAsync("online_users");
        var onlineUserIds = onlineUsers.Select(v => (int)v).ToHashSet();

        foreach (var msg in messages)
        {
            if (!onlineUserIds.Contains(msg.UserId))
            {
                msg.UserStatus = "Offline";
            }
        }

        return Ok(messages);
    }

    [HttpPost("upload")]
    public async Task<ActionResult<List<AttachmentDto>>> UploadFiles(List<Microsoft.AspNetCore.Http.IFormFile> files, [FromForm] int channelId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify user is in channel
        var channel = await _context.Channels.FindAsync(channelId);
        if (channel == null) return NotFound("Channel not found");

        if (channel.IsPrivate)
        {
            var isChannelMember = await _context.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId);
            if (!isChannelMember) return Forbid();
        }
        else
        {
            var isMember = await _context.WorkspaceMembers
                .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);
            if (!isMember) return Forbid();
        }

        // Create a temporary message for the attachments
        var message = new Message
        {
            ChannelId = channelId,
            UserId = userId,
            Content = "", // Content will be updated when message is sent
            Timestamp = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Upload files
        var attachments = await _fileUploadService.UploadFilesAsync(files, message.Id, userId);

        return Ok(attachments);
    }

    [HttpGet("attachment/{attachmentId}")]
    public async Task<IActionResult> DownloadAttachment(int attachmentId)
    {
        _logger.LogInformation("DownloadAttachment called: AttachmentId={AttachmentId}, User={UserId}", attachmentId, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Check if user can access this attachment
        var canAccess = await _fileUploadService.CanAccessAttachmentAsync(attachmentId, userId);
        _logger.LogInformation("CanAccessAttachment: AttachmentId={AttachmentId}, UserId={UserId}, CanAccess={CanAccess}", attachmentId, userId, canAccess);
        if (!canAccess) return Forbid();

        var attachment = await _fileUploadService.GetAttachmentAsync(attachmentId);
        if (attachment == null)
        {
            _logger.LogWarning("Attachment not found: AttachmentId={AttachmentId}", attachmentId);
            return NotFound("Attachment not found");
        }

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.FileUrl.TrimStart('/'));
        _logger.LogInformation("File path: FilePath={FilePath}, Exists={Exists}", filePath, System.IO.File.Exists(filePath));

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogError("File not found on disk: FilePath={FilePath}", filePath);
            return NotFound("File not found");
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        _logger.LogInformation("Returning file: FileName={FileName}, Size={Size}, ContentType={ContentType}", attachment.FileName, fileBytes.Length, attachment.FileType);
        return File(fileBytes, attachment.FileType, attachment.FileName);
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SharpTalk.Api.Data;
using SharpTalk.Api.Hubs;
using SharpTalk.Api.Services;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;

namespace SharpTalk.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly FileUploadService _fileUploadService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IDatabase _redis;

    public UserController(ApplicationDbContext context, IWebHostEnvironment environment, IConfiguration configuration, FileUploadService fileUploadService, IHubContext<ChatHub> hubContext, IConnectionMultiplexer redis)
    {
        _context = context;
        _environment = environment;
        _configuration = configuration;
        _fileUploadService = fileUploadService;
        _hubContext = hubContext;
        _redis = redis.GetDatabase();
    }

    [HttpGet("profile")]
    public async Task<ActionResult<UserInfo>> GetCurrentUserProfile()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        return Ok(new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            StartOnHome = user.StartOnHome,
            AutoOpenLastChannel = user.AutoOpenLastChannel
        });
    }

    [HttpPut("profile")]
    public async Task<ActionResult<UserInfo>> UpdateUserProfile([FromBody] UserInfo userInfo)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        user.Status = userInfo.Status;

        if (!string.IsNullOrEmpty(userInfo.AvatarUrl))
        {
            user.AvatarUrl = userInfo.AvatarUrl;
        }

        user.StartOnHome = userInfo.StartOnHome;
        user.AutoOpenLastChannel = userInfo.AutoOpenLastChannel;

        await _context.SaveChangesAsync();

        await BroadcastStatusChange(userId, user.Status);

        return Ok(new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            StartOnHome = user.StartOnHome,
            AutoOpenLastChannel = user.AutoOpenLastChannel
        });
    }

    [HttpPost("avatar")]
    public async Task<ActionResult<string>> UploadAvatar(IFormFile avatar)
    {
        if (avatar == null || avatar.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(avatar.ContentType.ToLower()))
        {
            return BadRequest("Invalid file type. Allowed types: JPEG, PNG, GIF, WebP");
        }

        // Validate file size
        var maxAvatarFileSize = int.Parse(_configuration["FileUploadSettings:MaxAvatarFileSizeBytes"] ?? "2097152");
        if (avatar.Length > maxAvatarFileSize)
        {
            return BadRequest($"File size too large. Maximum size: {maxAvatarFileSize / (1024 * 1024)}MB");
        }

        // Get user to retrieve username
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        // Sanitize username to prevent path traversal attacks
        var sanitizedUsername = SanitizeUsername(user.Username);

        // Process avatar using FileUploadService (resizing, compression, etc.)
        var avatarUrl = await _fileUploadService.ProcessAvatarAsync(avatar, sanitizedUsername);

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/avatars/"))
        {
            var oldPath = Path.Combine(_environment.ContentRootPath, "wwwroot", user.AvatarUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldPath))
            {
                System.IO.File.Delete(oldPath);
            }

            // Try to delete the user's avatar directory if it's empty
            var oldUserDir = Path.GetDirectoryName(oldPath);
            if (!string.IsNullOrEmpty(oldUserDir) && Directory.Exists(oldUserDir))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(oldUserDir).Any())
                    {
                        Directory.Delete(oldUserDir);
                    }
                }
                catch
                {
                    // Ignore errors when deleting empty directory
                }
            }
        }

        // Update user record
        user.AvatarUrl = avatarUrl;
        await _context.SaveChangesAsync();

        return Ok(new { avatarUrl });
    }

    /// <summary>
    /// Sanitizes username to prevent path traversal attacks
    /// </summary>
    private string SanitizeUsername(string username)
    {
        // Remove any path traversal characters
        var sanitized = username.Replace("..", "")
                                 .Replace("/", "")
                                 .Replace("\\", "")
                                 .Replace(":", "")
                                 .Replace("*", "")
                                 .Replace("?", "")
                                 .Replace("\"", "")
                                 .Replace("<", "")
                                 .Replace(">", "")
                                 .Replace("|", "");

        // Remove leading/trailing whitespace and dots
        sanitized = sanitized.Trim().Trim('.');

        // If sanitization results in empty string, use a default
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "user";
        }

        return sanitized;
    }

    [HttpPut("status")]
    public async Task<ActionResult<string>> UpdateStatus([FromBody] UpdateStatusRequest request)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        var validStatuses = new[] { "Online", "Away", "Offline" };
        if (!validStatuses.Contains(request.Status))
        {
            return BadRequest("Invalid status. Valid values: Online, Away, Offline");
        }

        user.Status = request.Status;
        await _context.SaveChangesAsync();

        await BroadcastStatusChange(userId, user.Status);

        return Ok(user.Status);
    }

    private async Task BroadcastStatusChange(int userId, string status)
    {
        if (await _redis.SetContainsAsync("online_users", userId))
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var statusDto = new UserStatusDto
            {
                UserId = userId,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl,
                Status = status
            };

            var workspaceIds = await _context.WorkspaceMembers
                .Where(wm => wm.UserId == userId)
                .Select(wm => wm.WorkspaceId)
                .ToListAsync();

            foreach (var workspaceId in workspaceIds)
            {
                await _hubContext.Clients.Group($"workspace_{workspaceId}").SendAsync("UserStatusChanged", statusDto);
            }
        }
    }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = "Online";
}

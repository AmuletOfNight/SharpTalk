using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Shared.DTOs;

namespace SharpTalk.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public UserController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
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
            Status = user.Status
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

        await _context.SaveChangesAsync();

        return Ok(new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status
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

        // Validate file size (max 2MB)
        const int maxFileSize = 2 * 1024 * 1024;
        if (avatar.Length > maxFileSize)
        {
            return BadRequest("File size too large. Maximum size: 2MB");
        }

        // Get user to retrieve username
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("User not found");
        }

        // Sanitize username to prevent path traversal attacks
        var sanitizedUsername = SanitizeUsername(user.Username);

        // Create user-specific directory
        var userAvatarPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "uploads", "avatars", sanitizedUsername);
        if (!Directory.Exists(userAvatarPath))
        {
            Directory.CreateDirectory(userAvatarPath);
        }

        // Generate random filename using GUID
        var extension = Path.GetExtension(avatar.FileName).ToLower();
        var randomFilename = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(userAvatarPath, randomFilename);

        // Save file
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await avatar.CopyToAsync(stream);
        }

        // Generate URL with username folder
        var avatarUrl = $"/uploads/avatars/{sanitizedUsername}/{randomFilename}";

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

        return Ok(user.Status);
    }
}

public class UpdateStatusRequest
{
    public string Status { get; set; } = "Online";
}

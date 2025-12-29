using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Shared.DTOs;
using System.Security.Claims;

namespace SharpTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessageController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MessageController(ApplicationDbContext context)
    {
        _context = context;
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

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);

        if (!isMember)
        {
            return Forbid();
        }

        var messages = await _context.Messages
            .Where(m => m.ChannelId == channelId)
            .Include(m => m.User) // Include user to get username
            .OrderBy(m => m.Timestamp)
            .Take(50) // Limit to last 50 for now
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ChannelId = m.ChannelId,
                UserId = m.UserId,
                Username = m.User.Username,
                Content = m.Content,
                Timestamp = m.Timestamp
            })
            .ToListAsync();

        return Ok(messages);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using System.Security.Claims;

namespace SharpTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChannelController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ChannelController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("{workspaceId}")]
    public async Task<ActionResult<List<ChannelDto>>> GetChannels(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // internal check: is user member of workspace?
        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (!isMember)
        {
            return Forbid();
        }

        var channels = await _context.Channels
            .Where(c => c.WorkspaceId == workspaceId && (!c.IsPrivate || _context.ChannelMembers.Any(cm => cm.ChannelId == c.Id && cm.UserId == userId)))
            .Select(c => new ChannelDto
            {
                Id = c.Id,
                WorkspaceId = c.WorkspaceId,
                Name = c.Name,
                Description = c.Description,
                IsPrivate = c.IsPrivate
            })
            .ToListAsync();

        return Ok(channels);
    }

    [HttpPost]
    public async Task<ActionResult<ChannelDto>> CreateChannel(CreateChannelRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify membership and ownership
        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId);

        if (workspace == null)
        {
            return NotFound("Workspace not found");
        }

        if (workspace.OwnerId != userId)
        {
            return Forbid();
        }

        // Ideally check roles here (e.g. only Admins can create channels?), allowing all members for now.

        var channel = new Channel
        {
            WorkspaceId = request.WorkspaceId,
            Name = request.Name,
            Description = request.Description,
            IsPrivate = request.IsPrivate
        };

        _context.Channels.Add(channel);
        await _context.SaveChangesAsync();

        // Add creator as a member
        var channelMember = new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = userId
        };
        _context.ChannelMembers.Add(channelMember);
        await _context.SaveChangesAsync();

        return Ok(new ChannelDto
        {
            Id = channel.Id,
            WorkspaceId = channel.WorkspaceId,
            Name = channel.Name,
            Description = channel.Description,
            IsPrivate = channel.IsPrivate
        });
    }
}

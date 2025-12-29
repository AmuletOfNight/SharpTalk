using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using System.Security.Claims;

namespace SharpTalk.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class WorkspaceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public WorkspaceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<WorkspaceDto>>> GetMyWorkspaces()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspaces = await _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId)
            .Include(wm => wm.Workspace)
            .Select(wm => new WorkspaceDto
            {
                Id = wm.Workspace.Id,
                Name = wm.Workspace.Name,
                OwnerId = wm.Workspace.OwnerId,
                MemberCount = wm.Workspace.Members.Count
            })
            .ToListAsync();

        return Ok(workspaces);
    }

    [HttpPost]
    public async Task<ActionResult<WorkspaceDto>> CreateWorkspace(CreateWorkspaceRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = new Workspace
        {
            Name = request.Name,
            OwnerId = userId
        };

        _context.Workspaces.Add(workspace);
        await _context.SaveChangesAsync();

        var member = new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = "Owner"
        };
        _context.WorkspaceMembers.Add(member);

        // Default General Channel
        var generalChannel = new Channel
        {
            WorkspaceId = workspace.Id,
            Name = "general",
            Description = "General discussion for everyone",
            IsPrivate = false
        };
        _context.Channels.Add(generalChannel);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMyWorkspaces), new { id = workspace.Id }, new WorkspaceDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            OwnerId = workspace.OwnerId,
            MemberCount = 1
        });
    }
    [HttpPost("invite")]
    public async Task<IActionResult> InviteUser(InviteUserRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(request.WorkspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != userId)
        {
            return Forbid();
        }

        var userToInvite = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (userToInvite == null) return NotFound("User not found");

        var exists = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == request.WorkspaceId && wm.UserId == userToInvite.Id);

        if (exists) return BadRequest("User is already a member");

        var member = new WorkspaceMember
        {
            WorkspaceId = request.WorkspaceId,
            UserId = userToInvite.Id,
            Role = "Member"
        };

        _context.WorkspaceMembers.Add(member);
        await _context.SaveChangesAsync();

        return Ok();
    }
}

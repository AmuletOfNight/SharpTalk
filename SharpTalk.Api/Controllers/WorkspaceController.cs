using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;
using System.Security.Claims;

namespace SharpTalk.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class WorkspaceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConnectionMultiplexer _redis;

    public WorkspaceController(ApplicationDbContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis;
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
                Description = wm.Workspace.Description,
                OwnerId = wm.Workspace.OwnerId,
                MemberCount = wm.Workspace.Members.Count,
                CreatedAt = wm.Workspace.CreatedAt
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
            Description = workspace.Description,
            OwnerId = workspace.OwnerId,
            MemberCount = 1,
            CreatedAt = workspace.CreatedAt
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

    [HttpGet("{workspaceId}/members")]
    public async Task<ActionResult<List<UserStatusDto>>> GetWorkspaceMembers(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (!isMember) return Forbid();

        var members = await _context.WorkspaceMembers
            .Where(wm => wm.WorkspaceId == workspaceId)
            .Include(wm => wm.User)
            .Select(wm => new UserStatusDto
            {
                UserId = wm.UserId,
                Username = wm.User.Username,
                AvatarUrl = wm.User.AvatarUrl,
                Status = wm.User.Status
            })
            .ToListAsync();

        var db = _redis.GetDatabase();
        var onlineUsers = await db.SetMembersAsync("online_users");
        var onlineUserIds = onlineUsers.Select(v => (int)v).ToHashSet();

        foreach (var member in members)
        {
            if (!onlineUserIds.Contains(member.UserId))
            {
                member.Status = "Offline";
            }
        }

        return Ok(members);
    }

    [HttpGet("{workspaceId}/members-detailed")]
    public async Task<ActionResult<List<WorkspaceMemberDto>>> GetWorkspaceMembersDetailed(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (!isMember) return Forbid();

        var members = await _context.WorkspaceMembers
            .Where(wm => wm.WorkspaceId == workspaceId)
            .Include(wm => wm.User)
            .Select(wm => new WorkspaceMemberDto
            {
                Id = wm.Id,
                UserId = wm.UserId,
                Username = wm.User.Username,
                Role = wm.Role,
                JoinedAt = wm.JoinedAt,
                IsOnline = false // Will be updated from Redis
            })
            .ToListAsync();

        var db = _redis.GetDatabase();
        var onlineUsers = await db.SetMembersAsync("online_users");
        var onlineUserIds = onlineUsers.Select(v => (int)v).ToHashSet();

        foreach (var member in members)
        {
            member.IsOnline = onlineUserIds.Contains(member.UserId);
            member.IsCurrentUser = member.UserId == userId;
        }

        return Ok(members);
    }

    [HttpPut("{workspaceId}/rename")]
    public async Task<IActionResult> RenameWorkspace(int workspaceId, RenameWorkspaceRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != userId)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.NewName))
        {
            return BadRequest("Workspace name cannot be empty");
        }

        workspace.Name = request.NewName;
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPut("{workspaceId}/description")]
    public async Task<IActionResult> UpdateDescription(int workspaceId, UpdateWorkspaceDescriptionRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != userId)
        {
            return Forbid();
        }

        workspace.Description = request.Description;
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("{workspaceId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int workspaceId, int userId)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != currentUserId)
        {
            return Forbid();
        }

        if (userId == currentUserId)
        {
            return BadRequest("Cannot remove yourself from workspace. Transfer ownership first.");
        }

        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (member == null) return NotFound("Member not found");

        _context.WorkspaceMembers.Remove(member);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("{workspaceId}/leave")]
    public async Task<IActionResult> LeaveWorkspace(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId == userId)
        {
            return BadRequest("Cannot leave workspace as owner. Transfer ownership first.");
        }

        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (member == null) return NotFound("Member not found");

        _context.WorkspaceMembers.Remove(member);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("{workspaceId}")]
    public async Task<IActionResult> DeleteWorkspace(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != userId)
        {
            return Forbid();
        }

        _context.Workspaces.Remove(workspace);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{workspaceId}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(int workspaceId, TransferOwnershipRequest request)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != currentUserId)
        {
            return Forbid();
        }

        if (request.NewOwnerId == currentUserId)
        {
            return BadRequest("Cannot transfer ownership to yourself");
        }

        var newOwnerMember = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == request.NewOwnerId);

        if (newOwnerMember == null) return NotFound("New owner is not a member of this workspace");

        var currentOwnerMember = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == currentUserId);

        if (currentOwnerMember == null) return NotFound("Current owner member not found");

        // Update workspace owner
        workspace.OwnerId = request.NewOwnerId;

        // Update roles
        newOwnerMember.Role = "Owner";
        currentOwnerMember.Role = "Member";

        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPut("{workspaceId}/members/{userId}/role")]
    public async Task<IActionResult> UpdateMemberRole(int workspaceId, int userId, UpdateMemberRoleRequest request)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != currentUserId)
        {
            return Forbid();
        }

        if (userId == currentUserId)
        {
            return BadRequest("Cannot change your own role");
        }

        var member = await _context.WorkspaceMembers
            .FirstOrDefaultAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (member == null) return NotFound("Member not found");

        if (request.NewRole != "Owner" && request.NewRole != "Member")
        {
            return BadRequest("Invalid role. Must be 'Owner' or 'Member'");
        }

        member.Role = request.NewRole;
        await _context.SaveChangesAsync();

        return Ok();
    }
}

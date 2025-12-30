using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _cache;

    public WorkspaceController(ApplicationDbContext context, IConnectionMultiplexer redis, IMemoryCache cache)
    {
        _context = context;
        _redis = redis;
        _cache = cache;
    }

    [HttpPost("reorder")]
    public async Task<IActionResult> ReorderWorkspaces([FromBody] List<int> workspaceIds)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var members = await _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId)
            .ToListAsync();

        foreach (var member in members)
        {
            var index = workspaceIds.IndexOf(member.WorkspaceId);
            if (index != -1)
            {
                member.OrderIndex = index;
            }
        }

        await _context.SaveChangesAsync();
        return Ok();
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
                OrderIndex = wm.OrderIndex,
                CreatedAt = wm.Workspace.CreatedAt
            })
            .OrderBy(w => w.OrderIndex)
            .ToListAsync();

        return Ok(workspaces);
    }

    [HttpGet("{workspaceId}")]
    public async Task<ActionResult<WorkspaceDto>> GetWorkspaceById(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var isMember = await _context.WorkspaceMembers
            .AsNoTracking()
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (!isMember) return Forbid();

        var workspace = await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        return Ok(new WorkspaceDto
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            OwnerId = workspace.OwnerId,
            MemberCount = workspace.Members.Count,
            CreatedAt = workspace.CreatedAt
        });
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

        var workspace = await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != userId)
        {
            return Forbid();
        }

        var userToInvite = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username);
        if (userToInvite == null) return NotFound("User not found");

        var exists = await _context.WorkspaceMembers
            .AsNoTracking()
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
            .AsNoTracking()
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (!isMember) return Forbid();

        var cacheKey = $"workspace_members_{workspaceId}";
        
        if (_cache.TryGetValue(cacheKey, out List<UserStatusDto>? cachedMembers) && cachedMembers != null)
        {
            // Update online status from Redis (gracefully handle Redis failures)
            try
            {
                var db = _redis.GetDatabase();
                var onlineUsers = await db.SetMembersAsync("online_users");
                var onlineUserIds = onlineUsers.Select(v => (int)v).ToHashSet();

                foreach (var member in cachedMembers)
                {
                    member.Status = onlineUserIds.Contains(member.UserId) ? "Online" : "Offline";
                }
            }
            catch (RedisException)
            {
                // Redis unavailable, keep default status
            }

            return Ok(cachedMembers);
        }

        var members = await _context.WorkspaceMembers
            .AsNoTracking()
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

        // Get online status from Redis (gracefully handle Redis failures)
        try
        {
            var db2 = _redis.GetDatabase();
            var onlineUsers2 = await db2.SetMembersAsync("online_users");
            var onlineUserIds2 = onlineUsers2.Select(v => (int)v).ToHashSet();

            foreach (var member in members)
            {
                if (!onlineUserIds2.Contains(member.UserId))
                {
                    member.Status = "Offline";
                }
            }
        }
        catch (RedisException)
        {
            // Redis unavailable, keep default status from database
        }

        _cache.Set(cacheKey, members, TimeSpan.FromMinutes(5));

        return Ok(members);
    }

    [HttpGet("{workspaceId}/members-detailed")]
    public async Task<ActionResult<List<WorkspaceMemberDto>>> GetWorkspaceMembersDetailed(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var isMember = await _context.WorkspaceMembers
            .AsNoTracking()
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (!isMember) return Forbid();

        var cacheKey = $"workspace_members_detailed_{workspaceId}";
        
        if (_cache.TryGetValue(cacheKey, out List<WorkspaceMemberDto>? cachedMembers) && cachedMembers != null)
        {
            // Update online status from Redis (gracefully handle Redis failures)
            try
            {
                var db = _redis.GetDatabase();
                var onlineUsers = await db.SetMembersAsync("online_users");
                var onlineUserIds = onlineUsers.Select(v => (int)v).ToHashSet();

                foreach (var member in cachedMembers)
                {
                    member.IsOnline = onlineUserIds.Contains(member.UserId);
                    member.IsCurrentUser = member.UserId == userId;
                }
            }
            catch (RedisException)
            {
                // Redis unavailable, keep default status
                foreach (var member in cachedMembers)
                {
                    member.IsCurrentUser = member.UserId == userId;
                }
            }

            return Ok(cachedMembers);
        }

        var members = await _context.WorkspaceMembers
            .AsNoTracking()
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

        // Get online status from Redis (gracefully handle Redis failures)
        try
        {
            var db2 = _redis.GetDatabase();
            var onlineUsers2 = await db2.SetMembersAsync("online_users");
            var onlineUserIds2 = onlineUsers2.Select(v => (int)v).ToHashSet();

            foreach (var member in members)
            {
                member.IsOnline = onlineUserIds2.Contains(member.UserId);
                member.IsCurrentUser = member.UserId == userId;
            }
        }
        catch (RedisException)
        {
            // Redis unavailable, keep default status
            foreach (var member in members)
            {
                member.IsCurrentUser = member.UserId == userId;
            }
        }

        _cache.Set(cacheKey, members, TimeSpan.FromMinutes(5));

        return Ok(members);
    }

    [HttpPut("{workspaceId}/rename")]
    public async Task<IActionResult> RenameWorkspace(int workspaceId, RenameWorkspaceRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
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

        // Invalidate cache
        _cache.Remove($"workspace_members_{workspaceId}");
        _cache.Remove($"workspace_members_detailed_{workspaceId}");

        return Ok();
    }

    [HttpPut("{workspaceId}/description")]
    public async Task<IActionResult> UpdateDescription(int workspaceId, UpdateWorkspaceDescriptionRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
        if (workspace == null) return NotFound("Workspace not found");

        if (workspace.OwnerId != userId)
        {
            return Forbid();
        }

        workspace.Description = request.Description;
        await _context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove($"workspace_members_{workspaceId}");
        _cache.Remove($"workspace_members_detailed_{workspaceId}");

        return Ok();
    }

    [HttpDelete("{workspaceId}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int workspaceId, int userId)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
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

        // Invalidate cache
        _cache.Remove($"workspace_members_{workspaceId}");
        _cache.Remove($"workspace_members_detailed_{workspaceId}");

        return Ok();
    }

    [HttpDelete("{workspaceId}/leave")]
    public async Task<IActionResult> LeaveWorkspace(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
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

        // Invalidate cache
        _cache.Remove($"workspace_members_{workspaceId}");
        _cache.Remove($"workspace_members_detailed_{workspaceId}");

        return Ok();
    }

    [HttpDelete("{workspaceId}")]
    public async Task<IActionResult> DeleteWorkspace(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
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

        var workspace = await _context.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
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

        // Invalidate cache
        _cache.Remove($"workspace_members_{workspaceId}");
        _cache.Remove($"workspace_members_detailed_{workspaceId}");

        return Ok();
    }

    [HttpPut("{workspaceId}/members/{userId}/role")]
    public async Task<IActionResult> UpdateMemberRole(int workspaceId, int userId, UpdateMemberRoleRequest request)
    {
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == workspaceId);
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

        // Invalidate cache
        _cache.Remove($"workspace_members_{workspaceId}");
        _cache.Remove($"workspace_members_detailed_{workspaceId}");

        return Ok();
    }
}

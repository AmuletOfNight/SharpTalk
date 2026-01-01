using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using SharpTalk.Shared.Enums;
using StackExchange.Redis;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using SharpTalk.Api.Hubs;

namespace SharpTalk.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class WorkspaceController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _cache;
    private readonly IHubContext<ChatHub> _hubContext;

    public WorkspaceController(ApplicationDbContext context, IConnectionMultiplexer redis, IMemoryCache cache, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _redis = redis;
        _cache = cache;
        _hubContext = hubContext;
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

        var pendingInvite = await _context.WorkspaceInvitations
            .AsNoTracking()
            .AnyAsync(wi => wi.WorkspaceId == request.WorkspaceId && wi.InviteeId == userToInvite.Id && wi.Status == InvitationStatus.Pending);

        if (pendingInvite) return BadRequest("User already has a pending invitation");

        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = request.WorkspaceId,
            InviterId = userId,
            InviteeId = userToInvite.Id,
            Type = InvitationType.Direct,
            Status = InvitationStatus.Pending
        };

        _context.WorkspaceInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("invite-link")]
    public async Task<ActionResult<string>> CreateInviteLink(CreateInviteLinkRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var workspace = await _context.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId);

        if (workspace == null) return NotFound("Workspace not found");
        if (workspace.OwnerId != userId) return Forbid();

        var code = Guid.NewGuid().ToString("N");
        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = request.WorkspaceId,
            InviterId = userId,
            Type = InvitationType.Link,
            Status = InvitationStatus.Pending,
            Code = code,
            MaxUses = request.MaxUses,
            ExpiresAt = request.ExpiresAt,
        };

        _context.WorkspaceInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        return Ok(code);
    }

    [HttpGet("{workspaceId}/invitations")]
    public async Task<ActionResult<List<WorkspaceInvitationDto>>> GetWorkspaceInvitations(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var workspace = await _context.Workspaces.FindAsync(workspaceId);

        if (workspace == null) return NotFound();
        if (workspace.OwnerId != userId) return Forbid();

        var invitations = await _context.WorkspaceInvitations
            .AsNoTracking()
            .Where(wi => wi.WorkspaceId == workspaceId && wi.Status == InvitationStatus.Pending) // Show only pending/active
            .Include(wi => wi.Invitee)
            .OrderByDescending(wi => wi.CreatedAt)
            .Select(wi => new WorkspaceInvitationDto
            {
                Id = wi.Id,
                WorkspaceId = wi.WorkspaceId,
                InviterId = wi.InviterId,
                InviteeId = wi.InviteeId,
                InviteeUsername = wi.Invitee != null ? wi.Invitee.Username : null,
                Type = wi.Type,
                Code = wi.Code,
                MaxUses = wi.MaxUses,
                UseCount = wi.UseCount,
                ExpiresAt = wi.ExpiresAt,
                CreatedAt = wi.CreatedAt
            })
            .ToListAsync();

        return Ok(invitations);
    }

    [HttpDelete("invitations/{invitationId}")]
    public async Task<IActionResult> RevokeInvitation(int invitationId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var invitation = await _context.WorkspaceInvitations
            .Include(wi => wi.Workspace)
            .FirstOrDefaultAsync(wi => wi.Id == invitationId);

        if (invitation == null) return NotFound();
        if (invitation.Workspace.OwnerId != userId) return Forbid();

        invitation.Status = InvitationStatus.Revoked;
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

        // Invalidate cache in memory
        _cache.Remove($"workspace_members_{workspaceId}");
        _cache.Remove($"workspace_members_detailed_{workspaceId}");

        // Invalidate channel access cache in Redis
        try
        {
            var db = _redis.GetDatabase();

            // Invalidate user workspace list cache
            await ChatHub.InvalidateUserWorkspaceCache(db, userId);

            // Get all channels for this workspace
            var channelIds = await _context.Channels
                .Where(c => c.WorkspaceId == workspaceId)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var channelId in channelIds)
            {
                await ChatHub.InvalidateChannelAccessCache(db, userId, channelId);
            }

            // Notify the removed user via SignalR to force UI update/redirect
            // We find the user's connection(s) via the user-specific group or direct message
            // Since we can't easily target a specific user ID without a custom user provider or connection mapping that might be complex to access here without IHubContext
            // Actually, SignalR allows sending to a user ID if the IUserIdProvider is set up (which it is by default using NameIdentifier)
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UserRemovedFromWorkspace", workspaceId);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the request
            Console.WriteLine($"Error invalidating cache or notifying user: {ex.Message}");
        }

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

        try
        {
            var db = _redis.GetDatabase();
            await ChatHub.InvalidateUserWorkspaceCache(db, userId);

            // Get all channels for this workspace
            var channelIds = await _context.Channels
                .Where(c => c.WorkspaceId == workspaceId)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var channelId in channelIds)
            {
                await ChatHub.InvalidateChannelAccessCache(db, userId, channelId);
            }

            // Notify the user via SignalR to force UI update/redirect in other tabs
            await _hubContext.Clients.User(userId.ToString()).SendAsync("UserRemovedFromWorkspace", workspaceId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error invalidating cache or notifying user: {ex.Message}");
        }

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

        // 1. Delete Workspace Invitations
        var invitations = await _context.WorkspaceInvitations
            .Where(wi => wi.WorkspaceId == workspaceId)
            .ToListAsync();
        _context.WorkspaceInvitations.RemoveRange(invitations);

        // 2. Delete Workspace Members
        var workspaceMembers = await _context.WorkspaceMembers
            .Where(wm => wm.WorkspaceId == workspaceId)
            .ToListAsync();
        _context.WorkspaceMembers.RemoveRange(workspaceMembers);

        // 3. Get Channels to delete related data
        var channels = await _context.Channels
            .Where(c => c.WorkspaceId == workspaceId)
            .ToListAsync();

        var channelIds = channels.Select(c => c.Id).ToList();

        // 4. Delete Messages (Attachments and Reactions should cascade via EF Core or DB config, but loading messages ensures EF Core tracks delete)
        // Optimizing by fetching IDs only if we were using ExecuteDelete, but for RemoveRange we need entities or stubs.
        var messages = await _context.Messages
            .Where(m => channelIds.Contains(m.ChannelId))
            .ToListAsync();
        _context.Messages.RemoveRange(messages);

        // 5. Delete Channel Members
        var channelMembers = await _context.ChannelMembers
            .Where(cm => channelIds.Contains(cm.ChannelId))
            .ToListAsync();
        _context.ChannelMembers.RemoveRange(channelMembers);

        // 6. Delete Channels
        _context.Channels.RemoveRange(channels);

        // 7. Delete Workspace
        _context.Workspaces.Remove(workspace);

        await _context.SaveChangesAsync();

        // Invalidate caches
        try
        {
            var db = _redis.GetDatabase();
            foreach (var member in workspaceMembers)
            {
                await ChatHub.InvalidateUserWorkspaceCache(db, member.UserId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error invalidating cache: {ex.Message}");
        }

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

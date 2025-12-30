using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;
using System.Security.Claims;

namespace SharpTalk.Api.Controllers;

[ApiController]
[Authorize]
public abstract class BaseController : ControllerBase
{
    protected readonly ApplicationDbContext _context;
    protected readonly IDatabase? _redis;
    protected readonly ILogger _logger;

    protected BaseController(ApplicationDbContext context, IConnectionMultiplexer redis, ILogger logger)
    {
        _context = context;
        _logger = logger;
        try
        {
            _redis = redis.GetDatabase();
        }
        catch (RedisException)
        {
            _redis = null;
        }
    }

    /// <summary>
    /// Gets the current user ID from the JWT claims
    /// </summary>
    protected int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            throw new UnauthorizedAccessException("User ID not found in claims");
        }
        return int.Parse(userIdClaim);
    }

    /// <summary>
    /// Checks if the current user is a member of the specified workspace
    /// </summary>
    protected async Task<bool> IsWorkspaceMemberAsync(int workspaceId, int? userId = null)
    {
        userId ??= GetCurrentUserId();
        return await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);
    }

    /// <summary>
    /// Checks if the current user is a member of the specified channel
    /// </summary>
    protected async Task<bool> IsChannelMemberAsync(int channelId, int? userId = null)
    {
        userId ??= GetCurrentUserId();
        return await _context.ChannelMembers
            .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId);
    }

    /// <summary>
    /// Checks if the current user is the owner of the specified workspace
    /// </summary>
    protected async Task<bool> IsWorkspaceOwnerAsync(int workspaceId, int? userId = null)
    {
        userId ??= GetCurrentUserId();
        return await _context.Workspaces
            .AnyAsync(w => w.Id == workspaceId && w.OwnerId == userId);
    }

    /// <summary>
    /// Gets the effective status of a user by checking if they're actually online in Redis
    /// </summary>
    protected async Task<string> GetEffectiveStatusAsync(int userId, string preferredStatus)
    {
        if (_redis == null) return "Offline";
        var isOnline = await _redis.SetContainsAsync("online_users", userId);
        return isOnline ? preferredStatus : "Offline";
    }

    /// <summary>
    /// Gets a set of online user IDs from Redis
    /// </summary>
    protected async Task<HashSet<int>> GetOnlineUserIdsAsync()
    {
        if (_redis == null) return new HashSet<int>();
        var onlineUsers = await _redis.SetMembersAsync("online_users");
        return onlineUsers.Select(v => (int)v).ToHashSet();
    }

    /// <summary>
    /// Updates user statuses in a collection based on online status from Redis
    /// </summary>
    protected async Task UpdateUserStatusesAsync<T>(ICollection<T> items, Func<T, int> getUserId, Func<T, string, T> setStatus)
    {
        var onlineUserIds = await GetOnlineUserIdsAsync();
        foreach (var item in items)
        {
            var userId = getUserId(item);
            var currentStatus = typeof(T).GetProperty("Status")?.GetValue(item)?.ToString() ?? "Offline";
            var newStatus = onlineUserIds.Contains(userId) ? currentStatus : "Offline";
            setStatus(item, newStatus);
        }
    }

    /// <summary>
    /// Validates that a workspace exists and the user is a member
    /// Returns the workspace if valid, null otherwise
    /// </summary>
    protected async Task<Workspace?> ValidateWorkspaceAccessAsync(int workspaceId, int? userId = null)
    {
        userId ??= GetCurrentUserId();
        
        var workspace = await _context.Workspaces.FindAsync(workspaceId);
        if (workspace == null)
        {
            return null;
        }

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        return isMember ? workspace : null;
    }

    /// <summary>
    /// Validates that a channel exists and the user has access
    /// Returns the channel if valid, null otherwise
    /// </summary>
    protected async Task<Channel?> ValidateChannelAccessAsync(int channelId, int? userId = null)
    {
        userId ??= GetCurrentUserId();
        
        var channel = await _context.Channels.FindAsync(channelId);
        if (channel == null)
        {
            return null;
        }

        if (channel.IsPrivate)
        {
            var isChannelMember = await _context.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId);
            return isChannelMember ? channel : null;
        }
        else
        {
            var isWorkspaceMember = await _context.WorkspaceMembers
                .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);
            return isWorkspaceMember ? channel : null;
        }
    }

    /// <summary>
    /// Checks if two users share any workspace
    /// </summary>
    protected async Task<bool> UsersShareWorkspaceAsync(int userId1, int userId2)
    {
        var user1Workspaces = _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId1)
            .Select(wm => wm.WorkspaceId);
        
        var user2Workspaces = _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId2)
            .Select(wm => wm.WorkspaceId);

        return await user1Workspaces.Intersect(user2Workspaces).AnyAsync();
    }
}

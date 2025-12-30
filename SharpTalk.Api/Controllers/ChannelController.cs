using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using System.Security.Claims;
using SharpTalk.Shared.Enums;
using StackExchange.Redis;

namespace SharpTalk.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChannelController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IDatabase? _redis;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public ChannelController(ApplicationDbContext context, IConnectionMultiplexer redis, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
        try
        {
            _redis = redis.GetDatabase();
        }
        catch (RedisException)
        {
            _redis = null;
        }
    }

    [HttpGet("{workspaceId}")]
    public async Task<ActionResult<List<ChannelDto>>> GetChannels(int workspaceId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var cacheKey = $"channels:workspace:{workspaceId}:user:{userId}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out List<ChannelDto>? cachedChannels))
        {
            return Ok(cachedChannels);
        }

        // internal check: is user member of workspace?
        var isMember = await _context.WorkspaceMembers
            .AsNoTracking()
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (!isMember)
        {
            return Forbid();
        }

        // Exclude DM channels from workspace list - DMs should only be accessible via global DMs list
        var channels = await _context.Channels
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId
                     && c.Type != ChannelType.Direct  // Exclude DM channels
                     && (!c.IsPrivate || _context.ChannelMembers.Any(cm => cm.ChannelId == c.Id && cm.UserId == userId)))
            .Select(c => new ChannelDto
            {
                Id = c.Id,
                WorkspaceId = c.WorkspaceId,
                Name = c.Name,
                Description = c.Description ?? string.Empty,
                IsPrivate = c.IsPrivate,
                Type = c.Type
            })
            .ToListAsync();

        // Cache the result
        _cache.Set(cacheKey, channels, _cacheDuration);

        return Ok(channels);
    }

    [HttpPost]
    public async Task<ActionResult<ChannelDto>> CreateChannel(CreateChannelRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify membership and ownership
        var workspace = await _context.Workspaces
            .AsNoTracking()
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
            IsPrivate = request.IsPrivate,
            Type = request.IsPrivate ? ChannelType.Private : ChannelType.Public
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
            IsPrivate = channel.IsPrivate,
            Type = channel.Type
        });
    }

    [HttpGet("dms")]
    public async Task<ActionResult<List<ChannelDto>>> GetDirectMessages()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var cacheKey = $"dms:user:{userId}";

        // Try to get from cache first (shorter duration for DMs as they change frequently)
        if (_cache.TryGetValue(cacheKey, out List<ChannelDto>? cachedDms))
        {
            return Ok(cachedDms);
        }

        // Fetch all DMs involving the user (Global + Legacy)
        var rawChannels = await _context.Channels
            .AsNoTracking()
            .Include(c => c.Members)
            .ThenInclude(m => m.User)
            .Where(c => c.Type == ChannelType.Direct && c.Members.Any(m => m.UserId == userId))
            .Select(c => new
            {
                Channel = c,
                TargetMember = c.Members.FirstOrDefault(m => m.UserId != userId),
                LastMessage = c.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault()
            })
            .ToListAsync();

        if (!rawChannels.Any())
        {
            return Ok(new List<ChannelDto>());
        }

        // Get my workspace IDs
        var myWorkspaceIds = (await _context.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => wm.UserId == userId)
            .Select(wm => wm.WorkspaceId)
            .ToListAsync()).ToHashSet();

        // Get all target user IDs
        var targetUserIds = rawChannels
            .Where(x => x.TargetMember != null)
            .Select(x => x.TargetMember!.UserId)
            .Distinct()
            .ToList();

        // Fetch workspace memberships for all target users
        var targetMemberships = await _context.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => targetUserIds.Contains(wm.UserId))
            .Select(wm => new { wm.UserId, wm.WorkspaceId })
            .ToListAsync();

        var targetWorkspaceMap = targetMemberships
            .GroupBy(wm => wm.UserId)
            .ToDictionary(g => g.Key, g => g.Select(wm => wm.WorkspaceId).ToHashSet());

        // In-memory grouping to coalesce duplicates and calculate CanMessage
        var groupedChannels = rawChannels
            .Where(x => x.TargetMember != null)
            .GroupBy(x => x.TargetMember!.UserId)
            .Select(g =>
            {
                var best = g.OrderByDescending(x => x.LastMessage?.Timestamp)
                            .ThenBy(x => x.Channel.WorkspaceId == null)
                            .ThenByDescending(x => x.Channel.Id)
                            .First();

                var targetUserId = best.TargetMember!.UserId;
                var canMessage = false;

                // Check workspace overlap
                if (targetWorkspaceMap.TryGetValue(targetUserId, out var targetWorkspaces))
                {
                    canMessage = myWorkspaceIds.Overlaps(targetWorkspaces);
                }

                return new ChannelDto
                {
                    Id = best.Channel.Id,
                    WorkspaceId = best.Channel.WorkspaceId,
                    Name = best.TargetMember!.User.Username,
                    AvatarUrl = best.TargetMember!.User.AvatarUrl,
                    Description = best.LastMessage?.Content ?? string.Empty,
                    IsPrivate = true,
                    Type = ChannelType.Direct,
                    CanMessage = canMessage,
                    UserStatus = best.TargetMember!.User.Status, // Will be corrected below
                    TargetUserId = targetUserId
                };
            })
            .OrderByDescending(c => c.WorkspaceId == null)
            .ToList();

        // Correct the UserStatus by checking Redis for actual online status
        foreach (var channel in groupedChannels)
        {
            if (channel.TargetUserId.HasValue)
            {
                channel.UserStatus = await GetEffectiveStatus(channel.TargetUserId.Value, channel.UserStatus);
            }
        }

        // Cache the result with shorter duration for DMs
        _cache.Set(cacheKey, groupedChannels, TimeSpan.FromMinutes(2));

        return Ok(groupedChannels);
    }

    [HttpPost("dm")]
    public async Task<ActionResult<ChannelDto>> StartDirectMessage(CreateDirectMessageRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // If WorkspaceId is provided, we can use it to verify they are in the same workspace,
        // but the resulting DM will be global (WorkspaceId = null).
        // Or we just check if they share *any* workspace.

        bool canMessage = false;

        if (request.WorkspaceId.HasValue)
        {
            var isMember = await _context.WorkspaceMembers
               .AsNoTracking()
               .AnyAsync(wm => wm.WorkspaceId == request.WorkspaceId && wm.UserId == userId);

            var targetIsMember = await _context.WorkspaceMembers
               .AsNoTracking()
               .AnyAsync(wm => wm.WorkspaceId == request.WorkspaceId && wm.UserId == request.TargetUserId);

            if (isMember && targetIsMember) canMessage = true;
        }
        else
        {
            // Check if they share ANY workspace
            var myWorkspaces = _context.WorkspaceMembers.AsNoTracking().Where(wm => wm.UserId == userId).Select(wm => wm.WorkspaceId);
            var targetWorkspaces = _context.WorkspaceMembers.AsNoTracking().Where(wm => wm.UserId == request.TargetUserId).Select(wm => wm.WorkspaceId);

            if (await myWorkspaces.Intersect(targetWorkspaces).AnyAsync())
            {
                canMessage = true;
            }
        }

        if (!canMessage) return BadRequest("You can only DM users you share a workspace with.");

        // Check for ANY existing DM - STRONGLY prefer Global DMs (WorkspaceId = null)
        // to ensure users have a single unified DM conversation regardless of where they start it
        var existingChannel = await _context.Channels
            .AsNoTracking()
            .Include(c => c.Members)
            .Where(c => c.Type == ChannelType.Direct
                     && c.Members.Any(m => m.UserId == userId)
                     && c.Members.Any(m => m.UserId == request.TargetUserId))
            .OrderByDescending(c => c.WorkspaceId == null)  // Global DMs first
            .ThenByDescending(c => c.Id)  // Newest first
            .FirstOrDefaultAsync();

        // Resolve name for existing DM (the other user) - use AsNoTracking for read-only
        var targetUser = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == request.TargetUserId)
            .Select(u => new { u.Username, u.AvatarUrl, u.Status })
            .FirstOrDefaultAsync();

        var targetUsername = targetUser?.Username ?? "Unknown";
        var targetAvatar = targetUser?.AvatarUrl;

        if (existingChannel != null)
        {
            // Get last message for description if possible, or empty - use AsNoTracking
            var lastMsg = await _context.Messages
                .AsNoTracking()
                .Where(m => m.ChannelId == existingChannel.Id)
                .OrderByDescending(m => m.Timestamp)
                .Select(m => m.Content)
                .FirstOrDefaultAsync();

            return Ok(new ChannelDto
            {
                Id = existingChannel.Id,
                WorkspaceId = existingChannel.WorkspaceId,
                Name = targetUsername,
                AvatarUrl = targetAvatar,
                Description = lastMsg ?? string.Empty,
                IsPrivate = true,
                Type = ChannelType.Direct,
                CanMessage = canMessage, // Computed earlier
                UserStatus = await GetEffectiveStatus(request.TargetUserId, targetUser?.Status ?? "Offline"),
                TargetUserId = request.TargetUserId
            });
        }

        // Create new Global DM
        var channel = new Channel
        {
            WorkspaceId = null, // Global
            Name = "dm",
            IsPrivate = true,
            Type = ChannelType.Direct
        };

        _context.Channels.Add(channel);
        await _context.SaveChangesAsync();

        _context.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = channel.Id, UserId = userId },
            new ChannelMember { ChannelId = channel.Id, UserId = request.TargetUserId }
        );

        await _context.SaveChangesAsync();

        return Ok(new ChannelDto
        {
            Id = channel.Id,
            WorkspaceId = null,
            Name = targetUsername,
            AvatarUrl = targetAvatar,
            Description = string.Empty,
            IsPrivate = true,
            Type = ChannelType.Direct,
            CanMessage = canMessage,
            UserStatus = await GetEffectiveStatus(request.TargetUserId, targetUser?.Status ?? "Offline"),
            TargetUserId = request.TargetUserId
        });
    }

    /// <summary>
    /// Check if a global DM exists with the specified target user
    /// </summary>
    [HttpGet("dm/check/{targetUserId}")]
    public async Task<ActionResult<ChannelDto?>> CheckExistingDM(int targetUserId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Find existing global DM (WorkspaceId = null) with this user
        var existingChannel = await _context.Channels
            .AsNoTracking()
            .Include(c => c.Members)
            .ThenInclude(m => m.User)
            .Where(c => c.Type == ChannelType.Direct
                     && c.WorkspaceId == null  // Only global DMs
                     && c.Members.Any(m => m.UserId == userId)
                     && c.Members.Any(m => m.UserId == targetUserId))
            .Select(c => new
            {
                Channel = c,
                TargetMember = c.Members.FirstOrDefault(m => m.UserId != userId),
                LastMessage = c.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        if (existingChannel == null || existingChannel.TargetMember == null)
        {
            return Ok(new ChannelDto());
        }

        var targetUser = existingChannel.TargetMember.User;
        var channel = existingChannel.Channel;
        var lastMsg = existingChannel.LastMessage;

        var result = new ChannelDto
        {
            Id = channel.Id,
            WorkspaceId = null,
            Name = targetUser.Username,
            AvatarUrl = targetUser.AvatarUrl,
            Description = lastMsg?.Content ?? string.Empty,
            IsPrivate = true,
            Type = ChannelType.Direct,
            CanMessage = true,
            UserStatus = await GetEffectiveStatus(targetUserId, targetUser.Status),
            TargetUserId = targetUserId
        };

        // Cache the DM check result
        var cacheKey = $"dm:check:{userId}:{targetUserId}";
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

        return Ok(result);
    }

    /// <summary>
    /// Gets the effective status of a user by checking if they're actually online in Redis.
    /// Returns their preferred status (Online/Away) if connected, otherwise "Offline".
    /// </summary>
    private async Task<string> GetEffectiveStatus(int userId, string preferredStatus)
    {
        if (_redis == null)
        {
            // Redis unavailable, return preferred status
            return preferredStatus;
        }

        try
        {
            // Check if user is in the online_users Redis set
            var isOnline = await _redis.SetContainsAsync("online_users", userId);

            // If they're not connected, they're offline regardless of their preference
            if (!isOnline)
            {
                return "Offline";
            }

            // If they are connected, return their preferred status
            return preferredStatus;
        }
        catch (RedisException)
        {
            // Redis unavailable, return preferred status
            return preferredStatus;
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared;
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

        var result = new List<ChannelDto>();

        // Fetch all 1:1 DMs involving the user (Global + Legacy)
        var rawDirectChannels = await _context.Channels
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

        if (rawDirectChannels.Any())
        {
            // Get my workspace IDs
            var myWorkspaceIds = (await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(wm => wm.UserId == userId)
                .Select(wm => wm.WorkspaceId)
                .ToListAsync()).ToHashSet();

            // Get all target user IDs
            var targetUserIds = rawDirectChannels
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
            var directChannels = rawDirectChannels
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

                    var channelDto = new ChannelDto
                    {
                        Id = best.Channel.Id,
                        WorkspaceId = best.Channel.WorkspaceId,
                        Name = best.TargetMember!.User.Username,
                        AvatarUrl = best.TargetMember!.User.AvatarUrl,
                        Description = best.LastMessage?.Content ?? string.Empty,
                        IsPrivate = true,
                        Type = ChannelType.Direct,
                        CanMessage = canMessage,
                        UserStatus = best.TargetMember!.User.Status,
                        TargetUserId = targetUserId
                    };

                    return channelDto;
                })
                .OrderByDescending(c => c.WorkspaceId == null)
                .ToList();

            // Correct the UserStatus by checking Redis for actual online status
            foreach (var channel in directChannels)
            {
                if (channel.TargetUserId.HasValue)
                {
                    channel.UserStatus = await GetEffectiveStatus(channel.TargetUserId.Value, channel.UserStatus);
                }
            }

            result.AddRange(directChannels);
        }

        // Fetch all group DMs involving the user
        var rawGroupChannels = await _context.Channels
            .AsNoTracking()
            .Where(c => c.Type == ChannelType.Group && c.Members.Any(m => m.UserId == userId))
            .Select(c => new
            {
                Channel = c,
                MemberCount = c.Members.Count,
                LastMessage = c.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault()
            })
            .ToListAsync();

        if (rawGroupChannels.Any())
        {
            var groupChannels = rawGroupChannels
                .OrderByDescending(x => x.LastMessage?.Timestamp)
                .ThenByDescending(x => x.Channel.Id)
                .Select(x => new ChannelDto
                {
                    Id = x.Channel.Id,
                    WorkspaceId = x.Channel.WorkspaceId,
                    Name = x.Channel.Name,
                    Description = x.LastMessage?.Content ?? string.Empty,
                    IsPrivate = true,
                    Type = ChannelType.Group,
                    CanMessage = true,
                    MemberCount = x.MemberCount
                })
                .ToList();

            result.AddRange(groupChannels);
        }

        // Cache the result with shorter duration for DMs
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));

        return Ok(result);
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

    #region Group DM Endpoints

    /// <summary>
    /// Create a new group DM with multiple members
    /// </summary>
    [HttpPost("groupdm")]
    public async Task<ActionResult<ChannelDto>> CreateGroupDM(CreateGroupDMRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Validate member count
        if (request.MemberIds.Count < ChatConstants.GroupDMMinMembers - 1 ||
            request.MemberIds.Count > ChatConstants.GroupDMMaxAdditionalMembers)
        {
            return BadRequest($"Group must have between {ChatConstants.GroupDMMinMembers} and {ChatConstants.GroupDMMaxMembers} members.");
        }

        // Check for duplicates in member IDs
        if (request.MemberIds.Distinct().Count() != request.MemberIds.Count)
        {
            return BadRequest("Duplicate members are not allowed.");
        }

        // Verify all users share at least one workspace with the creator
        var myWorkspaceIds = await _context.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => wm.UserId == userId)
            .Select(wm => wm.WorkspaceId)
            .ToListAsync();

        foreach (var targetUserId in request.MemberIds)
        {
            var targetWorkspaces = await _context.WorkspaceMembers
                .AsNoTracking()
                .Where(wm => wm.UserId == targetUserId)
                .Select(wm => wm.WorkspaceId)
                .ToListAsync();

            if (!myWorkspaceIds.Intersect(targetWorkspaces).Any())
            {
                var targetUser = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == targetUserId)
                    .Select(u => u.Username)
                    .FirstOrDefaultAsync();
                return BadRequest($"You must share a workspace with {targetUser ?? "this user"} to add them to a group DM.");
            }
        }

        // Check for existing group DM with the same member set
        var allMemberIds = request.MemberIds.Concat(new[] { userId }).OrderBy(id => id).ToList();
        var existingGroups = await _context.Channels
            .AsNoTracking()
            .Include(c => c.Members)
            .Where(c => c.Type == ChannelType.Group && c.WorkspaceId == null)
            .Select(c => new
            {
                Channel = c,
                MemberIds = c.Members.Select(m => m.UserId).OrderBy(id => id).ToList()
            })
            .ToListAsync();

        var existingGroup = existingGroups.FirstOrDefault(g =>
            g.MemberIds.SequenceEqual(allMemberIds));

        if (existingGroup != null)
        {
            // Return existing group
            return await GetGroupDMResponse(existingGroup.Channel.Id);
        }

        // Generate group name if not provided
        var groupName = request.Name;
        if (string.IsNullOrEmpty(groupName))
        {
            var memberUsernames = new List<string>();
            foreach (var memberId in request.MemberIds.Take(3))
            {
                var username = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == memberId)
                    .Select(u => u.Username)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(username))
                {
                    memberUsernames.Add(username);
                }
            }

            if (memberUsernames.Count == allMemberIds.Count - 1)
            {
                groupName = string.Join(", ", memberUsernames);
            }
            else if (memberUsernames.Count >= 2)
            {
                var remaining = allMemberIds.Count - 1 - 2;
                groupName = remaining > 0
                    ? $"{memberUsernames[0]}, {memberUsernames[1]}, and {remaining} others"
                    : $"{memberUsernames[0]} and {memberUsernames[1]}";
            }
            else if (memberUsernames.Count == 1)
            {
                groupName = $"{memberUsernames[0]} and {allMemberIds.Count - 2} others";
            }
            else
            {
                groupName = "Group DM";
            }
        }

        // Create new group DM
        var channel = new Channel
        {
            WorkspaceId = null, // Global
            Name = groupName,
            IsPrivate = true,
            Type = ChannelType.Group
        };

        _context.Channels.Add(channel);
        await _context.SaveChangesAsync();

        // Add all members
        var members = allMemberIds.Select(userIdToAdd => new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = userIdToAdd
        }).ToList();

        _context.ChannelMembers.AddRange(members);
        await _context.SaveChangesAsync();

        // Invalidate channel access cache for all members so they can immediately join via SignalR
        if (_redis != null)
        {
            foreach (var memberId in allMemberIds)
            {
                try
                {
                    await _redis.KeyDeleteAsync($"channel_access:{memberId}:{channel.Id}");
                }
                catch (RedisException)
                {
                    // Redis unavailable, skip caching
                }
            }
        }

        // Invalidate DM cache for all members
        foreach (var memberId in allMemberIds)
        {
            _cache.Remove($"dms:user:{memberId}");
        }

        return await GetGroupDMResponse(channel.Id);
    }

    /// <summary>
    /// Add a member to an existing group DM
    /// </summary>
    [HttpPost("groupdm/members")]
    public async Task<ActionResult<ChannelDto>> AddGroupMember(AddGroupMemberRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify the channel exists and is a group DM
        var channel = await _context.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId && c.Type == ChannelType.Group);

        if (channel == null)
        {
            return NotFound("Group DM not found.");
        }

        // Verify the requester is a member
        if (!channel.Members.Any(m => m.UserId == userId))
        {
            return Forbid();
        }

        // Check member limit
        if (channel.Members.Count >= ChatConstants.GroupDMMaxMembers)
        {
            return BadRequest($"Group DM cannot have more than {ChatConstants.GroupDMMaxMembers} members.");
        }

        // Check if user is already a member
        if (channel.Members.Any(m => m.UserId == request.UserId))
        {
            return BadRequest("User is already a member of this group.");
        }

        // Verify the new user shares at least one workspace with the requester
        var requesterWorkspaces = await _context.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => wm.UserId == userId)
            .Select(wm => wm.WorkspaceId)
            .ToListAsync();

        var targetWorkspaces = await _context.WorkspaceMembers
            .AsNoTracking()
            .Where(wm => wm.UserId == request.UserId)
            .Select(wm => wm.WorkspaceId)
            .ToListAsync();

        if (!requesterWorkspaces.Intersect(targetWorkspaces).Any())
        {
            return BadRequest("You must share a workspace with this user to add them to the group.");
        }

        // Add the member
        var newMember = new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = request.UserId
        };

        _context.ChannelMembers.Add(newMember);
        await _context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove($"dms:user:{userId}");
        _cache.Remove($"dms:user:{request.UserId}");

        return await GetGroupDMResponse(channel.Id);
    }

    /// <summary>
    /// Remove a member from an existing group DM
    /// </summary>
    [HttpDelete("groupdm/{channelId}/members/{targetUserId}")]
    public async Task<ActionResult<ChannelDto>> RemoveGroupMember(int channelId, int targetUserId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify the channel exists and is a group DM
        var channel = await _context.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ChannelType.Group);

        if (channel == null)
        {
            return NotFound("Group DM not found.");
        }

        // Verify the requester is a member
        if (!channel.Members.Any(m => m.UserId == userId))
        {
            return Forbid();
        }

        // Check if target is a member
        var targetMember = channel.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (targetMember == null)
        {
            return NotFound("User is not a member of this group.");
        }

        // Prevent removing the last member (minimum 2 required for group)
        if (channel.Members.Count <= 2)
        {
            return BadRequest("Cannot remove member. Group must have at least 2 members.");
        }

        // Remove the member
        _context.ChannelMembers.Remove(targetMember);
        await _context.SaveChangesAsync();

        // Invalidate cache for all members
        foreach (var member in channel.Members.Where(m => m.UserId != targetUserId))
        {
            _cache.Remove($"dms:user:{member.UserId}");
        }

        return await GetGroupDMResponse(channel.Id);
    }

    /// <summary>
    /// Update the name of a group DM
    /// </summary>
    [HttpPut("groupdm/{channelId}/name")]
    public async Task<ActionResult<ChannelDto>> UpdateGroupName(int channelId, UpdateGroupNameRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify the channel exists and is a group DM
        var channel = await _context.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ChannelType.Group);

        if (channel == null)
        {
            return NotFound("Group DM not found.");
        }

        // Verify the requester is a member
        if (!channel.Members.Any(m => m.UserId == userId))
        {
            return Forbid();
        }

        // Update the name
        channel.Name = request.Name;
        _context.Channels.Update(channel);
        await _context.SaveChangesAsync();

        // Invalidate cache for all members
        foreach (var member in channel.Members)
        {
            _cache.Remove($"dms:user:{member.UserId}");
        }

        return await GetGroupDMResponse(channel.Id);
    }

    /// <summary>
    /// Leave a group DM
    /// </summary>
    [HttpPost("groupdm/{channelId}/leave")]
    public async Task<ActionResult> LeaveGroup(int channelId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify the channel exists and is a group DM
        var channel = await _context.Channels
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ChannelType.Group);

        if (channel == null)
        {
            return NotFound("Group DM not found.");
        }

        // Verify the requester is a member
        var member = channel.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
        {
            return Forbid();
        }

        // Check if this is the last member
        if (channel.Members.Count == 1)
        {
            // Delete the group entirely
            _context.ChannelMembers.Remove(member);
            _context.Channels.Remove(channel);
            await _context.SaveChangesAsync();
        }
        else
        {
            // Just remove the member
            _context.ChannelMembers.Remove(member);
            await _context.SaveChangesAsync();

            // Invalidate cache for remaining members
            foreach (var remainingMember in channel.Members.Where(m => m.UserId != userId))
            {
                _cache.Remove($"dms:user:{remainingMember.UserId}");
            }
        }

        // Invalidate own cache
        _cache.Remove($"dms:user:{userId}");

        return Ok();
    }

    /// <summary>
    /// Get all members of a group DM
    /// </summary>
    [HttpGet("groupdm/{channelId}/members")]
    public async Task<ActionResult<List<GroupMemberDto>>> GetGroupMembers(int channelId)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

        // Verify the channel exists and is a group DM
        var channel = await _context.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Type == ChannelType.Group);

        if (channel == null)
        {
            return NotFound("Group DM not found.");
        }

        // Verify the requester is a member
        var isMember = await _context.ChannelMembers
            .AsNoTracking()
            .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId);

        if (!isMember)
        {
            return Forbid();
        }

        // Get all members with user info
        var members = await _context.ChannelMembers
            .AsNoTracking()
            .Include(cm => cm.User)
            .Where(cm => cm.ChannelId == channelId)
            .OrderBy(cm => cm.JoinedAt)
            .Select(cm => new GroupMemberDto
            {
                UserId = cm.User.Id,
                Username = cm.User.Username,
                AvatarUrl = cm.User.AvatarUrl,
                Status = cm.User.Status,
                JoinedAt = cm.JoinedAt
            })
            .ToListAsync();

        // Correct statuses from Redis
        foreach (var member in members)
        {
            member.Status = await GetEffectiveStatus(member.UserId, member.Status);
        }

        return Ok(members);
    }

    /// <summary>
    /// Get a group DM response with all details
    /// </summary>
    private async Task<ActionResult<ChannelDto>> GetGroupDMResponse(int channelId)
    {
        var channel = await _context.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == channelId);

        if (channel == null)
        {
            return NotFound("Group DM not found.");
        }

        // Get member count
        var memberCount = await _context.ChannelMembers
            .AsNoTracking()
            .CountAsync(cm => cm.ChannelId == channelId);

        // Get last message for description
        var lastMessage = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync();

        return Ok(new ChannelDto
        {
            Id = channel.Id,
            WorkspaceId = channel.WorkspaceId,
            Name = channel.Name,
            Description = lastMessage?.Content ?? string.Empty,
            IsPrivate = channel.IsPrivate,
            Type = ChannelType.Group,
            CanMessage = true,
            MemberCount = memberCount
        });
    }

    #endregion

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

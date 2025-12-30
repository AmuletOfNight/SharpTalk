using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;
using System.Security.Claims;
using SharpTalk.Shared.Enums;

namespace SharpTalk.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly IDatabase? _redis;
    private readonly ILogger<ChatHub> _logger;
    private static readonly TimeSpan _connectionExpiry = TimeSpan.FromMinutes(30);

    public ChatHub(ApplicationDbContext context, IConnectionMultiplexer redis, ILogger<ChatHub> logger)
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
            _logger.LogWarning("Redis not available, presence and caching features will be disabled");
        }
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId > 0 && _redis != null)
        {
            try
            {
                var userConnectionsKey = $"user_connections:{userId}";
                await _redis.SetAddAsync(userConnectionsKey, Context.ConnectionId);
                await _redis.KeyExpireAsync(userConnectionsKey, _connectionExpiry);

                // Only notify online if this is the first set addition (race-condition safe)
                if (await _redis.SetAddAsync("online_users", userId))
                {
                    var user = await _context.Users.FindAsync(userId);
                    var status = user?.Status ?? "Online";
                    await NotifyPresenceChanged(userId, status);
                    _logger.LogInformation("User {UserId} came online", userId);
                }
            }
            catch (RedisException)
            {
                _logger.LogWarning("Redis unavailable during OnConnectedAsync for user {UserId}", userId);
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId > 0 && _redis != null)
        {
            try
            {
                var userConnectionsKey = $"user_connections:{userId}";
                await _redis.SetRemoveAsync(userConnectionsKey, Context.ConnectionId);

                // Only notify offline if this was the last connection
                var connectionCount = await _redis.SetLengthAsync(userConnectionsKey);
                if (connectionCount == 0)
                {
                    await _redis.SetRemoveAsync("online_users", userId);
                    await NotifyPresenceChanged(userId, "Offline");
                    _logger.LogInformation("User {UserId} went offline", userId);
                }
            }
            catch (RedisException)
            {
                _logger.LogWarning("Redis unavailable during OnDisconnectedAsync for user {UserId}", userId);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private int GetUserId()
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out var userId) ? userId : 0;
    }

    private string GetUsername() => Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

    /// <summary>
    /// Cached channel membership check with Redis backing
    /// </summary>
    private async Task<bool> IsUserInChannel(int userId, int channelId)
    {
        // Try cache first if Redis is available
        if (_redis != null)
        {
            try
            {
                var cacheKey = $"channel_access:{userId}:{channelId}";
                var cached = await _redis.StringGetAsync(cacheKey);
                
                if (cached.HasValue)
                {
                    return cached == "1";
                }
            }
            catch (RedisException)
            {
                // Redis unavailable, skip caching
            }
        }

        var channel = await _context.Channels.FindAsync(channelId);
        if (channel == null) return false;

        bool hasAccess = false;

        // For global DMs (WorkspaceId == null), skip workspace check
        // For workspace channels, must be in workspace
        if (channel.WorkspaceId.HasValue)
        {
            hasAccess = await _context.WorkspaceMembers
                .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);
        }
        else
        {
            // Global channel - assume access pending ChannelMembers check
            hasAccess = true;
        }

        // If private, must be in ChannelMembers
        if (hasAccess && channel.IsPrivate)
        {
            hasAccess = await _context.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId);
        }

        // Cache result for 5 minutes if Redis is available
        if (_redis != null)
        {
            try
            {
                var cacheKey = $"channel_access:{userId}:{channelId}";
                await _redis.StringSetAsync(cacheKey, hasAccess ? "1" : "0", TimeSpan.FromMinutes(5));
            }
            catch (RedisException)
            {
                // Redis unavailable, skip caching
            }
        }
        
        return hasAccess;
    }

    /// <summary>
    /// Get user's workspace IDs with caching
    /// </summary>
    private async Task<List<int>> GetUserWorkspaceIds(int userId)
    {
        // Try cache first if Redis is available
        if (_redis != null)
        {
            try
            {
                var cacheKey = $"user_workspaces:{userId}";
                var cached = await _redis.StringGetAsync(cacheKey);
                
                if (cached.HasValue)
                {
                    return cached.ToString().Split(',').Select(int.Parse).ToList();
                }
            }
            catch (RedisException)
            {
                // Redis unavailable, skip caching
            }
        }

        var workspaceIds = await _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId)
            .Select(wm => wm.WorkspaceId)
            .ToListAsync();

        // Cache for 10 minutes if Redis is available
        if (_redis != null)
        {
            try
            {
                var cacheKey = $"user_workspaces:{userId}";
                await _redis.StringSetAsync(cacheKey, string.Join(',', workspaceIds), TimeSpan.FromMinutes(10));
            }
            catch (RedisException)
            {
                // Redis unavailable, skip caching
            }
        }
        
        return workspaceIds;
    }

    /// <summary>
    /// Invalidate user workspace cache when membership changes
    /// </summary>
    public static async Task InvalidateUserWorkspaceCache(IDatabase redis, int userId)
    {
        await redis.KeyDeleteAsync($"user_workspaces:{userId}");
    }

    /// <summary>
    /// Invalidate channel access cache when channel membership changes
    /// </summary>
    public static async Task InvalidateChannelAccessCache(IDatabase redis, int userId, int channelId)
    {
        await redis.KeyDeleteAsync($"channel_access:{userId}:{channelId}");
    }

    public async Task NotifyPresenceChanged(int userId, string? status = null)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;

        var statusDto = new UserStatusDto
        {
            UserId = userId,
            Username = user.Username,
            AvatarUrl = user.AvatarUrl,
            Status = status ?? user.Status
        };

        // Get cached workspace IDs
        var workspaceIds = await GetUserWorkspaceIds(userId);

        // Broadcast to all workspaces in parallel
        var tasks = workspaceIds.Select(workspaceId => 
            Clients.Group($"workspace_{workspaceId}").SendAsync("UserStatusChanged", statusDto)
        );
        await Task.WhenAll(tasks);
    }

    public async Task JoinWorkspace(int workspaceId)
    {
        var userId = GetUserId();
        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (isMember)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"workspace_{workspaceId}");
            _logger.LogDebug("User {UserId} joined workspace {WorkspaceId}", userId, workspaceId);
        }
        else
        {
            _logger.LogWarning("User {UserId} attempted to join workspace {WorkspaceId} without membership", userId, workspaceId);
        }
    }

    public async Task JoinChannel(int channelId)
    {
        var userId = GetUserId();

        if (!await IsUserInChannel(userId, channelId))
        {
            throw new HubException("Unauthorized access to channel.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());
        _logger.LogDebug("User {UserId} joined channel {ChannelId}", userId, channelId);
    }

    public async Task LeaveChannel(int channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());
        _logger.LogDebug("Connection {ConnectionId} left channel {ChannelId}", Context.ConnectionId, channelId);
    }

    public async Task SendMessage(int channelId, string content, List<int>? attachmentIds = null)
    {
        var userId = GetUserId();
        var username = GetUsername();

        if (!await IsUserInChannel(userId, channelId)) return;

        // [SECURITY] Extra validation for Global DMs to ensure shared workspace
        var channel = await _context.Channels.FindAsync(channelId);

        if (channel != null && channel.WorkspaceId == null && channel.IsPrivate && channel.Type == ChannelType.Direct)
        {
            // Find the other member
            var members = await _context.ChannelMembers
                .Where(cm => cm.ChannelId == channelId)
                .Select(cm => cm.UserId)
                .ToListAsync();

            var targetUserId = members.FirstOrDefault(id => id != userId);
            if (targetUserId > 0)
            {
                // Check shared workspace using cached data
                var myWorkspaces = await GetUserWorkspaceIds(userId);
                var targetWorkspaces = await GetUserWorkspaceIds(targetUserId);

                if (!myWorkspaces.Intersect(targetWorkspaces).Any())
                {
                    throw new HubException("You must be a part of at least one shared workspace to message with this person.");
                }
            }
        }

        // Get user avatar
        var user = await _context.Users.FindAsync(userId);
        var avatarUrl = user?.AvatarUrl;

        // Save to DB
        var message = new Message
        {
            ChannelId = channelId,
            UserId = userId,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // Load attachments if provided
        List<AttachmentDto> attachmentDtos = new List<AttachmentDto>();
        if (attachmentIds != null && attachmentIds.Any())
        {
            var attachments = await _context.Attachments
                .Where(a => attachmentIds.Contains(a.Id))
                .ToListAsync();

            // Update attachments with the message ID
            foreach (var attachment in attachments)
            {
                attachment.MessageId = message.Id;
            }
            await _context.SaveChangesAsync();

            attachmentDtos = attachments.Select(a => new AttachmentDto
            {
                Id = a.Id,
                MessageId = a.MessageId,
                FileName = a.FileName,
                FileUrl = a.FileUrl,
                FileType = a.FileType,
                FileSize = a.FileSize,
                CreatedAt = a.CreatedAt
            }).ToList();
        }

        var messageDto = new MessageDto
        {
            Id = message.Id,
            ChannelId = channelId,
            UserId = userId,
            Username = username,
            AvatarUrl = avatarUrl,
            UserStatus = user?.Status ?? "Offline",
            Content = content,
            Timestamp = message.Timestamp,
            Attachments = attachmentDtos
        };

        // Broadcast to the channel group
        await Clients.Group(channelId.ToString()).SendAsync("ReceiveMessage", messageDto);
    }

    public async Task SendTypingIndicator(int channelId, bool isTyping)
    {
        var userId = GetUserId();
        var username = GetUsername();
        await Clients.Group(channelId.ToString()).SendAsync("UserTyping", channelId, userId, username, isTyping);
    }
}

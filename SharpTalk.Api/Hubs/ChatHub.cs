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
    private readonly IDatabase _redis;

    public ChatHub(ApplicationDbContext context, IConnectionMultiplexer redis)
    {
        _context = context;
        _redis = redis.GetDatabase();
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId > 0)
        {
            var userConnectionsKey = $"user_connections:{userId}";
            await _redis.SetAddAsync(userConnectionsKey, Context.ConnectionId);

            // Only notify online if this is the first set addition (race-condition safe)
            if (await _redis.SetAddAsync("online_users", userId))
            {
                var user = await _context.Users.FindAsync(userId);
                var status = user?.Status ?? "Online";
                await NotifyPresenceChanged(userId, status);
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId > 0)
        {
            var userConnectionsKey = $"user_connections:{userId}";
            await _redis.SetRemoveAsync(userConnectionsKey, Context.ConnectionId);

            // Only notify offline if this was the last connection
            var connectionCount = await _redis.SetLengthAsync(userConnectionsKey);
            if (connectionCount == 0)
            {
                await _redis.SetRemoveAsync("online_users", userId);
                await NotifyPresenceChanged(userId, "Offline");
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

    private async Task<bool> IsUserInChannel(int userId, int channelId)
    {
        var channel = await _context.Channels.FindAsync(channelId);
        if (channel == null) return false;

        // Must be in workspace if it belongs to one
        if (channel.WorkspaceId.HasValue)
        {
            var inWorkspace = await _context.WorkspaceMembers
                .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);

            if (!inWorkspace) return false;
        }

        // If private, must be in ChannelMembers
        if (channel.IsPrivate)
        {
            return await _context.ChannelMembers
                .AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId);
        }

        return true;
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

        // Notify workspaces this user belongs to
        var workspaceIds = await _context.WorkspaceMembers
            .Where(wm => wm.UserId == userId)
            .Select(wm => wm.WorkspaceId)
            .ToListAsync();

        foreach (var workspaceId in workspaceIds)
        {
            await Clients.Group($"workspace_{workspaceId}").SendAsync("UserStatusChanged", statusDto);
        }
    }

    public async Task JoinWorkspace(int workspaceId)
    {
        var userId = GetUserId();
        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == userId);

        if (isMember)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"workspace_{workspaceId}");
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
    }

    public async Task LeaveChannel(int channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());
    }

    public async Task SendMessage(int channelId, string content, List<int> attachmentIds = null)
    {
        var userId = GetUserId();
        var username = GetUsername();

        // [SECURITY] Re-verify membership (optional optimization: assume JoinChannel checked it, but safer to check or rely on DB constraints)
        // For performance, we might rely on the fact they are in the group, but saving to DB requires validation.


        if (!await IsUserInChannel(userId, channelId)) return;

        // [SECURITY] Extra validation for Global DMs to ensure shared workspace
        var channel = await _context.Channels.FindAsync(channelId); // Re-fetch or pass in? IsUserInChannel fetched it... 
                                                                    // Optimization: Create a cached method or trust IsUserInChannel? 
                                                                    // IsUserInChannel only checks MEMBERSHIP. We need to check SHARED WORKSPACE if it's a global DM.

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
                // Check shared workspace
                var myWorkspaces = _context.WorkspaceMembers.Where(wm => wm.UserId == userId).Select(wm => wm.WorkspaceId);
                var targetWorkspaces = _context.WorkspaceMembers.Where(wm => wm.UserId == targetUserId).Select(wm => wm.WorkspaceId);

                if (!await myWorkspaces.Intersect(targetWorkspaces).AnyAsync())
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;
using System.Security.Claims;

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
            await _redis.SetAddAsync("online_users", userId);
            await NotifyPresenceChanged(userId, "Online");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId > 0)
        {
            await _redis.SetRemoveAsync("online_users", userId);
            await NotifyPresenceChanged(userId, "Offline");
        }
        await base.OnDisconnectedAsync(exception);
    }

    private int GetUserId()
    {
        var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdStr, out var userId) ? userId : 0;
    }

    private string GetUsername() => Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

    private async Task NotifyPresenceChanged(int userId, string status)
    {
        var username = GetUsername();
        var statusDto = new UserStatusDto
        {
            UserId = userId,
            Username = username,
            Status = status
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

        // [SECURITY] Verify user is member of the workspace for this channel
        var channel = await _context.Channels.FindAsync(channelId);
        if (channel == null)
        {
            throw new HubException("Channel not found.");
        }

        var isMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);

        if (!isMember)
        {
            throw new HubException("Unauthorized access to channel.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, channelId.ToString());
    }

    public async Task LeaveChannel(int channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channelId.ToString());
    }

    public async Task SendMessage(int channelId, string content)
    {
        var userId = GetUserId();
        var username = GetUsername();

        // [SECURITY] Re-verify membership (optional optimization: assume JoinChannel checked it, but safer to check or rely on DB constraints)
        // For performance, we might rely on the fact they are in the group, but saving to DB requires validation.

        var channel = await _context.Channels.FindAsync(channelId);
        if (channel == null) return;

        var isMember = await _context.WorkspaceMembers
             .AnyAsync(wm => wm.WorkspaceId == channel.WorkspaceId && wm.UserId == userId);

        if (!isMember) return;

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

        var messageDto = new MessageDto
        {
            Id = message.Id,
            ChannelId = channelId,
            UserId = userId,
            Username = username,
            Content = content,
            Timestamp = message.Timestamp
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using System.Security.Claims;

namespace SharpTalk.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationDbContext _context;

    public ChatHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task JoinChannel(int channelId)
    {
        var userId = int.Parse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

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
        var userId = int.Parse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
        var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

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
}

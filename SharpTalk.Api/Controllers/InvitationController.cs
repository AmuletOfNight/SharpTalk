using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpTalk.Api.Data;
using SharpTalk.Api.Entities;
using SharpTalk.Shared.DTOs;
using SharpTalk.Shared.Enums;
using System.Security.Claims;

namespace SharpTalk.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class InvitationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

    public InvitationController(ApplicationDbContext context, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<WorkspaceInvitationDto>>> GetMyInvitations()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var invitations = await _context.WorkspaceInvitations
            .AsNoTracking()
            .Where(wi => wi.InviteeId == userId && wi.Status == InvitationStatus.Pending && wi.Type == InvitationType.Direct)
            .Include(wi => wi.Workspace)
            .Include(wi => wi.Inviter)
            .OrderByDescending(wi => wi.CreatedAt)
            .Select(wi => new WorkspaceInvitationDto
            {
                Id = wi.Id,
                WorkspaceId = wi.WorkspaceId,
                WorkspaceName = wi.Workspace.Name,
                InviterId = wi.InviterId,
                InviterUsername = wi.Inviter.Username,
                InviteeId = wi.InviteeId,
                Type = wi.Type,
                Status = wi.Status,
                CreatedAt = wi.CreatedAt,
                ExpiresAt = wi.ExpiresAt
            })
            .ToListAsync();

        return Ok(invitations);
    }

    [Authorize]
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> AcceptInvitation(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var invitation = await _context.WorkspaceInvitations
            .Include(wi => wi.Workspace)
            .FirstOrDefaultAsync(wi => wi.Id == id);

        if (invitation == null) return NotFound("Invitation not found");
        if (invitation.InviteeId != userId) return Forbid();
        if (invitation.Status != InvitationStatus.Pending) return BadRequest("Invitation is not pending");

        // Check expiration
        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await _context.SaveChangesAsync();
            return BadRequest("Invitation has expired");
        }

        // Add user to workspace
        var alreadyMember = await _context.WorkspaceMembers
            .AnyAsync(wm => wm.WorkspaceId == invitation.WorkspaceId && wm.UserId == userId);

        if (!alreadyMember)
        {
            var member = new WorkspaceMember
            {
                WorkspaceId = invitation.WorkspaceId,
                UserId = userId,
                Role = "Member"
            };
            _context.WorkspaceMembers.Add(member);
        }

        invitation.Status = InvitationStatus.Accepted;
        await _context.SaveChangesAsync();

        // Invalidate cache
        _cache.Remove($"workspace_members_{invitation.WorkspaceId}");
        _cache.Remove($"workspace_members_detailed_{invitation.WorkspaceId}");

        return Ok();
    }

    [Authorize]
    [HttpPost("{id}/decline")]
    public async Task<IActionResult> DeclineInvitation(int id)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var invitation = await _context.WorkspaceInvitations
            .FirstOrDefaultAsync(wi => wi.Id == id);

        if (invitation == null) return NotFound("Invitation not found");
        if (invitation.InviteeId != userId) return Forbid();

        if (invitation.Status == InvitationStatus.Pending)
        {
            invitation.Status = InvitationStatus.Declined;
            await _context.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpGet("link/{code}")]
    public async Task<ActionResult<WorkspaceInvitationDto>> GetLinkInfo(string code)
    {
        var invitation = await _context.WorkspaceInvitations
            .AsNoTracking()
            .Include(wi => wi.Workspace)
            .Include(wi => wi.Inviter)
            .FirstOrDefaultAsync(wi => wi.Code == code && wi.Type == InvitationType.Link);

        if (invitation == null) return NotFound("Invitation not found");

        if (invitation.Status == InvitationStatus.Revoked)
        {
            return BadRequest("This invitation link has been revoked.");
        }

        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
        {
            return BadRequest("This invitation link has expired.");
        }

        if (invitation.MaxUses.HasValue && invitation.UseCount >= invitation.MaxUses.Value)
        {
            return BadRequest("This invitation link has reached its maximum usage limit.");
        }

        return Ok(new WorkspaceInvitationDto
        {
            WorkspaceId = invitation.WorkspaceId,
            WorkspaceName = invitation.Workspace.Name,
            InviterId = invitation.InviterId,
            InviterUsername = invitation.Inviter.Username,
            Code = invitation.Code,
            Type = invitation.Type,
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt
        });
    }

    [Authorize]
    [HttpPost("link/{code}/join")]
    public async Task<IActionResult> JoinByLink(string code)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // Transaction to ensure UseCount consistency
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var invitation = await _context.WorkspaceInvitations
                .FirstOrDefaultAsync(wi => wi.Code == code && wi.Type == InvitationType.Link);

            if (invitation == null) return NotFound("Invitation not found");

            if (invitation.Status == InvitationStatus.Revoked) return BadRequest("Invitation revoked");

            if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
            {
                invitation.Status = InvitationStatus.Expired;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return BadRequest("Invitation expired");
            }

            if (invitation.MaxUses.HasValue && invitation.UseCount >= invitation.MaxUses.Value)
            {
                return BadRequest("Invitation usage limit reached");
            }

            var alreadyMember = await _context.WorkspaceMembers
                .AnyAsync(wm => wm.WorkspaceId == invitation.WorkspaceId && wm.UserId == userId);

            if (alreadyMember)
            {
                return BadRequest("You are already a member of this workspace");
            }

            // Create member
            var member = new WorkspaceMember
            {
                WorkspaceId = invitation.WorkspaceId,
                UserId = userId,
                Role = "Member"
            };
            _context.WorkspaceMembers.Add(member);

            // Increment usage
            invitation.UseCount++;

            if (invitation.MaxUses.HasValue && invitation.UseCount >= invitation.MaxUses.Value)
            {
                // Optionally mark as expired or just rely on the check?
                // Let's leave status as Pending/Active unless revoked, but max usage check handles it.
                // Or we could have a "Consumed" status? But Link invites are reusable.
                // Logic above checks UseCount >= MaxUses.
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Invalidate cache
            _cache.Remove($"workspace_members_{invitation.WorkspaceId}");
            _cache.Remove($"workspace_members_detailed_{invitation.WorkspaceId}");

            return Ok(new WorkspaceDto
            {
                Id = invitation.WorkspaceId,
                // We'd need to fetch workspace details to return full DTO, but usually client just wants success
                // or we can return simple ID. WorkspaceController.GetMyWorkspaces will fetch it next.
            });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

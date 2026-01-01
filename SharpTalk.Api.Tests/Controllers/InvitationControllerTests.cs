using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SharpTalk.Api.Controllers;
using SharpTalk.Api.Entities;
using SharpTalk.Api.Tests.Helpers;
using SharpTalk.Shared.DTOs;
using SharpTalk.Shared.Enums;

namespace SharpTalk.Api.Tests.Controllers;

public class InvitationControllerTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly InvitationController _controller;
    private readonly Mock<IMemoryCache> _cacheMock;

    public InvitationControllerTests()
    {
        _dbHelper = TestDbContextHelper.Create();
        _cacheMock = new Mock<IMemoryCache>();
        _controller = new InvitationController(_dbHelper.Context, _cacheMock.Object);
    }

    private void SetupUser(int userId, string username = "testuser")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
    }

    [Fact]
    public async Task GetMyInvitations_ReturnsPendingInvitations()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Create an invitation for user 1
        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = 1,
            InviteeId = 1,
            InviterId = 2,
            Type = InvitationType.Direct,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _dbHelper.Context.WorkspaceInvitations.Add(invitation);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMyInvitations();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var invitations = okResult.Value.Should().BeAssignableTo<IEnumerable<WorkspaceInvitationDto>>().Subject;

        invitations.Should().ContainSingle();
        invitations.First().Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task AcceptInvitation_ValidInvite_AddsMemberAndUpdatesStatus()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2"); // User 2 will accept invite to Workspace 1

        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = 1,
            InviteeId = 2,
            InviterId = 1,
            Type = InvitationType.Direct,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _dbHelper.Context.WorkspaceInvitations.Add(invitation);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.AcceptInvitation(invitation.Id);

        // Assert
        result.Should().BeOfType<OkResult>();

        var updatedInvite = await _dbHelper.Context.WorkspaceInvitations.FindAsync(invitation.Id);
        updatedInvite!.Status.Should().Be(InvitationStatus.Accepted);

        var member = _dbHelper.Context.WorkspaceMembers
            .FirstOrDefault(m => m.WorkspaceId == 1 && m.UserId == 2);
        member.Should().NotBeNull();
    }

    [Fact]
    public async Task DeclineInvitation_ValidInvite_UpdatesStatus()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2");

        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = 1,
            InviteeId = 2,
            InviterId = 1,
            Type = InvitationType.Direct,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _dbHelper.Context.WorkspaceInvitations.Add(invitation);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.DeclineInvitation(invitation.Id);

        // Assert
        result.Should().BeOfType<OkResult>();

        var updatedInvite = await _dbHelper.Context.WorkspaceInvitations.FindAsync(invitation.Id);
        updatedInvite!.Status.Should().Be(InvitationStatus.Declined);
    }

    [Fact]
    public async Task JoinByLink_ValidCode_AddsMember()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2"); // User 2 joining

        var code = Guid.NewGuid().ToString("N");
        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = 1,
            InviterId = 1,
            Type = InvitationType.Link,
            Status = InvitationStatus.Pending,
            Code = code,
            CreatedAt = DateTime.UtcNow
        };
        _dbHelper.Context.WorkspaceInvitations.Add(invitation);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.JoinByLink(code);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;

        var member = _dbHelper.Context.WorkspaceMembers
            .FirstOrDefault(m => m.WorkspaceId == 1 && m.UserId == 2);
        member.Should().NotBeNull();

        var updatedInvite = await _dbHelper.Context.WorkspaceInvitations.FindAsync(invitation.Id);
        updatedInvite!.UseCount.Should().Be(1);
    }

    [Fact]
    public async Task JoinByLink_Expired_ReturnsBadRequest()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2");

        var code = Guid.NewGuid().ToString("N");
        var invitation = new WorkspaceInvitation
        {
            WorkspaceId = 1,
            InviterId = 1,
            Type = InvitationType.Link,
            Status = InvitationStatus.Pending,
            Code = code,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        _dbHelper.Context.WorkspaceInvitations.Add(invitation);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.JoinByLink(code);

        // Assert
        // Logic might check expiry and return BadRequest("Invitation expired")
        // and update status to Expired?
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Invitation expired");

        var updatedInvite = await _dbHelper.Context.WorkspaceInvitations.FindAsync(invitation.Id);
        updatedInvite!.Status.Should().Be(InvitationStatus.Expired);
    }
}

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
using StackExchange.Redis;

namespace SharpTalk.Api.Tests.Controllers;

public class WorkspaceControllerTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly WorkspaceController _controller;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IMemoryCache> _cacheMock;

    public WorkspaceControllerTests()
    {
        _dbHelper = TestDbContextHelper.Create();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _cacheMock = new Mock<IMemoryCache>();

        // Setup Redis mock
        var redisDatabaseMock = new Mock<IDatabase>();
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(redisDatabaseMock.Object);

        _controller = new WorkspaceController(_dbHelper.Context, _redisMock.Object, _cacheMock.Object);
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

    #region CreateWorkspace Tests

    [Fact]
    public async Task CreateWorkspace_WithValidData_CreatesWorkspaceWithDefaultChannel()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new CreateWorkspaceRequest
        {
            Name = "New Workspace",

        };

        // Act
        var result = await _controller.CreateWorkspace(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var workspace = okResult.Value.Should().BeOfType<WorkspaceDto>().Subject;

        workspace.Name.Should().Be("New Workspace");

        // Verify default channel was created
        var channels = _dbHelper.Context.Channels.Where(c => c.WorkspaceId == workspace.Id).ToList();
        channels.Should().Contain(c => c.Name == "general");
    }

    [Fact]
    public async Task CreateWorkspace_AddsOwnerAsMember()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new CreateWorkspaceRequest
        {
            Name = "New Workspace"
        };

        // Act
        var result = await _controller.CreateWorkspace(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var workspace = okResult.Value.Should().BeOfType<WorkspaceDto>().Subject;

        var member = _dbHelper.Context.WorkspaceMembers
            .FirstOrDefault(wm => wm.WorkspaceId == workspace.Id && wm.UserId == 1);

        member.Should().NotBeNull();
        member!.Role.Should().Be("Owner");
    }

    #endregion

    #region GetMyWorkspaces Tests

    [Fact]
    public async Task GetMyWorkspaces_ReturnsUserWorkspaces()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Act
        var result = await _controller.GetMyWorkspaces();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var workspaces = okResult.Value.Should().BeAssignableTo<IEnumerable<WorkspaceDto>>().Subject;

        workspaces.Should().NotBeEmpty();
        workspaces.Should().Contain(w => w.Name == "Test Workspace");
    }

    [Fact]
    public async Task GetMyWorkspaces_ReturnsEmptyForNewUser()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2"); // User 2 is not a member of any workspace

        // Act
        var result = await _controller.GetMyWorkspaces();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var workspaces = okResult.Value.Should().BeAssignableTo<IEnumerable<WorkspaceDto>>().Subject;

        workspaces.Should().BeEmpty();
    }

    #endregion

    #region InviteUser Tests

    [Fact]
    public async Task InviteUser_AddsMemberToWorkspace()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new InviteUserRequest
        {
            WorkspaceId = 1,
            Username = "testuser2"
        };

        // Act
        var result = await _controller.InviteUser(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        var member = _dbHelper.Context.WorkspaceMembers
            .FirstOrDefault(wm => wm.WorkspaceId == 1 && wm.UserId == 2);

        member.Should().NotBeNull();
        member!.Role.Should().Be("Member");
    }

    [Fact]
    public async Task InviteUser_WithNonexistentUser_ReturnsBadRequest()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new InviteUserRequest
        {
            WorkspaceId = 1,
            Username = "nonexistent"
        };

        // Act
        var result = await _controller.InviteUser(request);

        // Assert - Controller returns NotFound for nonexistent users
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task InviteUser_AlreadyMember_ReturnsBadRequest()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // First invite
        var request = new InviteUserRequest
        {
            WorkspaceId = 1,
            Username = "testuser2"
        };
        await _controller.InviteUser(request);

        // Act - Try to invite again
        var result = await _controller.InviteUser(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region RenameWorkspace Tests

    [Fact]
    public async Task RenameWorkspace_UpdatesName()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new RenameWorkspaceRequest
        {
            WorkspaceId = 1,
            NewName = "Renamed Workspace"
        };

        // Act
        var result = await _controller.RenameWorkspace(1, request);

        // Assert
        result.Should().BeOfType<OkResult>();

        var workspace = _dbHelper.Context.Workspaces.Find(1);
        workspace!.Name.Should().Be("Renamed Workspace");
    }

    [Fact]
    public async Task RenameWorkspace_NonOwner_ReturnsForbidden()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();

        // Add user2 as a member (not owner)
        _dbHelper.Context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = 1,
            UserId = 2,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            OrderIndex = 1
        });
        await _dbHelper.Context.SaveChangesAsync();

        SetupUser(2, "testuser2");

        var request = new RenameWorkspaceRequest
        {
            WorkspaceId = 1,
            NewName = "Renamed Workspace"
        };

        // Act
        var result = await _controller.RenameWorkspace(1, request);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region DeleteWorkspace Tests

    [Fact]
    public async Task DeleteWorkspace_DeletesWorkspace()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Act
        var result = await _controller.DeleteWorkspace(1);

        // Assert
        result.Should().BeOfType<OkResult>();

        var workspace = _dbHelper.Context.Workspaces.Find(1);
        workspace.Should().BeNull();
    }

    [Fact]
    public async Task DeleteWorkspace_NonOwner_ReturnsForbidden()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();

        // Add user2 as a member (not owner)
        _dbHelper.Context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = 1,
            UserId = 2,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            OrderIndex = 1
        });
        await _dbHelper.Context.SaveChangesAsync();

        SetupUser(2, "testuser2");

        // Act
        var result = await _controller.DeleteWorkspace(1);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region LeaveWorkspace Tests

    [Fact]
    public async Task LeaveWorkspace_RemovesCurrentUser()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();

        // Add user2 as a member
        _dbHelper.Context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = 1,
            UserId = 2,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            OrderIndex = 1
        });
        await _dbHelper.Context.SaveChangesAsync();

        SetupUser(2, "testuser2");

        // Act
        var result = await _controller.LeaveWorkspace(1);

        // Assert
        result.Should().BeOfType<OkResult>();

        var member = _dbHelper.Context.WorkspaceMembers
            .FirstOrDefault(wm => wm.WorkspaceId == 1 && wm.UserId == 2);

        member.Should().BeNull();
    }

    #endregion

    #region ReorderWorkspaces Tests

    [Fact]
    public async Task ReorderWorkspaces_UpdatesOrderIndex()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Create additional workspaces for reordering
        var workspace2 = new Workspace { Name = "Workspace 2", OwnerId = 1, CreatedAt = DateTime.UtcNow };
        var workspace3 = new Workspace { Name = "Workspace 3", OwnerId = 1, CreatedAt = DateTime.UtcNow };
        _dbHelper.Context.Workspaces.AddRange(workspace2, workspace3);
        await _dbHelper.Context.SaveChangesAsync();

        _dbHelper.Context.WorkspaceMembers.AddRange(
            new WorkspaceMember { WorkspaceId = workspace2.Id, UserId = 1, Role = "Owner", JoinedAt = DateTime.UtcNow, OrderIndex = 1 },
            new WorkspaceMember { WorkspaceId = workspace3.Id, UserId = 1, Role = "Owner", JoinedAt = DateTime.UtcNow, OrderIndex = 2 }
        );
        await _dbHelper.Context.SaveChangesAsync();

        var newOrder = new List<int> { workspace3.Id, 1, workspace2.Id };

        // Act
        var result = await _controller.ReorderWorkspaces(newOrder);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    #endregion
}

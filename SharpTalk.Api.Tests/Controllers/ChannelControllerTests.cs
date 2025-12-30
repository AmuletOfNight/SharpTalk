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
using StackExchange.Redis;

namespace SharpTalk.Api.Tests.Controllers;

public class ChannelControllerTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly ChannelController _controller;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDatabaseMock;
    private readonly Mock<IMemoryCache> _cacheMock;

    public ChannelControllerTests()
    {
        _dbHelper = TestDbContextHelper.Create();
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDatabaseMock = new Mock<IDatabase>();
        _cacheMock = new Mock<IMemoryCache>();
        
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDatabaseMock.Object);
        
        _controller = new ChannelController(_dbHelper.Context, _redisMock.Object, _cacheMock.Object);
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

    #region GetChannels Tests

    [Fact]
    public async Task GetChannels_ReturnsPublicChannels()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Act
        var result = await _controller.GetChannels(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var channels = okResult.Value.Should().BeAssignableTo<IEnumerable<ChannelDto>>().Subject;
        
        channels.Should().NotBeEmpty();
        channels.Should().Contain(c => c.Name == "general");
    }

    [Fact]
    public async Task GetChannels_NonMember_ReturnsForbidden()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2"); // User 2 is not a member

        // Act
        var result = await _controller.GetChannels(1);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetChannels_ReturnsPrivateChannelsUserIsMemberOf()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Create a private channel and add user as member
        var privateChannel = new Channel
        {
            WorkspaceId = 1,
            Name = "private-channel",
            IsPrivate = true,
            
        };
        _dbHelper.Context.Channels.Add(privateChannel);
        await _dbHelper.Context.SaveChangesAsync();

        _dbHelper.Context.ChannelMembers.Add(new ChannelMember
        {
            ChannelId = privateChannel.Id,
            UserId = 1,
            JoinedAt = DateTime.UtcNow
        });
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.GetChannels(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var channels = okResult.Value.Should().BeAssignableTo<IEnumerable<ChannelDto>>().Subject;
        
        channels.Should().Contain(c => c.Name == "private-channel");
    }

    #endregion

    #region CreateChannel Tests

    [Fact]
    public async Task CreateChannel_CreatesPublicChannel()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new CreateChannelRequest
        {
            WorkspaceId = 1,
            Name = "new-channel",
            IsPrivate = false
        };

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var channel = okResult.Value.Should().BeOfType<ChannelDto>().Subject;
        
        channel.Name.Should().Be("new-channel");
        channel.IsPrivate.Should().BeFalse();
    }

    [Fact]
    public async Task CreateChannel_PrivateChannel_AddsCreatorAsMember()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new CreateChannelRequest
        {
            WorkspaceId = 1,
            Name = "new-private-channel",
            IsPrivate = true
        };

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var channel = okResult.Value.Should().BeOfType<ChannelDto>().Subject;
        
        var member = _dbHelper.Context.ChannelMembers
            .FirstOrDefault(cm => cm.ChannelId == channel.Id && cm.UserId == 1);
        
        member.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateChannel_NonMember_ReturnsForbidden()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2"); // User 2 is not a workspace member

        var request = new CreateChannelRequest
        {
            WorkspaceId = 1,
            Name = "new-channel",
            IsPrivate = false
        };

        // Act
        var result = await _controller.CreateChannel(request);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion

    #region GetDirectMessages Tests

    [Fact]
    public async Task GetDirectMessages_ReturnsUserDMs()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");
        
        // Setup Redis mock for online users
        _redisDatabaseMock
            .Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());
        
        // Setup Redis mock for user status
        _redisDatabaseMock
            .Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Create a DM channel
        var dmChannel = new Channel
        {
            WorkspaceId = null, // Global DM
            Name = "DM",
            IsPrivate = true,
            Type = ChannelType.Direct,
            
        };
        _dbHelper.Context.Channels.Add(dmChannel);
        await _dbHelper.Context.SaveChangesAsync();

        // Add both users as members
        _dbHelper.Context.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = dmChannel.Id, UserId = 1, JoinedAt = DateTime.UtcNow },
            new ChannelMember { ChannelId = dmChannel.Id, UserId = 2, JoinedAt = DateTime.UtcNow }
        );
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.GetDirectMessages();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dms = okResult.Value.Should().BeAssignableTo<IEnumerable<ChannelDto>>().Subject;
        
        dms.Should().NotBeEmpty();
    }

    #endregion

    #region StartDirectMessage Tests

    [Fact]
    public async Task StartDirectMessage_CreatesNewDMChannel()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        // Add user2 to workspace to allow DM
        _dbHelper.Context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = 1,
            UserId = 2,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            OrderIndex = 1
        });
        await _dbHelper.Context.SaveChangesAsync();
        
        SetupUser(1, "testuser1");

        var request = new CreateDirectMessageRequest
        {
            TargetUserId = 2
        };

        // Act
        var result = await _controller.StartDirectMessage(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var channel = okResult.Value.Should().BeOfType<ChannelDto>().Subject;
        
        channel.Type.Should().Be(ChannelType.Direct);
    }

    [Fact]
    public async Task StartDirectMessage_ReturnsExistingDMIfExists()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        // Add user2 to workspace
        _dbHelper.Context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = 1,
            UserId = 2,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            OrderIndex = 1
        });
        await _dbHelper.Context.SaveChangesAsync();
        
        SetupUser(1, "testuser1");

        var request = new CreateDirectMessageRequest
        {
            TargetUserId = 2
        };

        // Create first DM
        var firstResult = await _controller.StartDirectMessage(request);
        var firstChannel = ((OkObjectResult)firstResult.Result!).Value as ChannelDto;

        // Act - Try to create again
        var secondResult = await _controller.StartDirectMessage(request);

        // Assert
        var okResult = secondResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        var secondChannel = okResult.Value.Should().BeOfType<ChannelDto>().Subject;
        
        secondChannel.Id.Should().Be(firstChannel!.Id);
    }

    #endregion
}

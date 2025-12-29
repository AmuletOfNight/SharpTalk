using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SharpTalk.Api.Controllers;
using SharpTalk.Api.Entities;
using SharpTalk.Api.Services;
using SharpTalk.Api.Tests.Helpers;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;

namespace SharpTalk.Api.Tests.Controllers;

public class MessageControllerTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly MessageController _controller;
    private readonly Mock<FileUploadService> _fileUploadServiceMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<MessageController>> _loggerMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDatabaseMock;

    public MessageControllerTests()
    {
        _dbHelper = TestDbContextHelper.Create();
        
        // Create mock configuration
        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(x => x["MessageSettings:MaxMessagesToRetrieve"]).Returns("50");
        
        // Create mock logger
        _loggerMock = new Mock<ILogger<MessageController>>();
        
        // Create mock Redis
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDatabaseMock = new Mock<IDatabase>();
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDatabaseMock.Object);
        
        // Setup Redis mock for online users
        _redisDatabaseMock
            .Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());
        
        // FileUploadService needs real dependencies, we'll create a partial mock
        var fileUploadLoggerMock = new Mock<ILogger<FileUploadService>>();
        var environmentMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        environmentMock.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
        
        _fileUploadServiceMock = new Mock<FileUploadService>(
            _dbHelper.Context, 
            environmentMock.Object, 
            _configurationMock.Object, 
            fileUploadLoggerMock.Object);

        _controller = new MessageController(
            _dbHelper.Context, 
            _fileUploadServiceMock.Object, 
            _configurationMock.Object, 
            _loggerMock.Object,
            _redisMock.Object);
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

    #region GetMessages Tests

    [Fact]
    public async Task GetMessages_ReturnsChannelMessages()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Add a message to the channel
        var message = new Message
        {
            ChannelId = 1,
            UserId = 1,
            Content = "Test message",
            Timestamp = DateTime.UtcNow
        };
        _dbHelper.Context.Messages.Add(message);
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMessages(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = okResult.Value.Should().BeAssignableTo<IEnumerable<MessageDto>>().Subject;
        
        messages.Should().NotBeEmpty();
        messages.Should().Contain(m => m.Content == "Test message");
    }

    [Fact]
    public async Task GetMessages_ChannelNotFound_ReturnsNotFound()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Act
        var result = await _controller.GetMessages(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMessages_ForPrivateChannel_ReturnsForbiddenIfNotMember()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        // Create a private channel (user 1 is not a member)
        var privateChannel = new Channel
        {
            WorkspaceId = 1,
            Name = "private-channel",
            IsPrivate = true,
            
        };
        _dbHelper.Context.Channels.Add(privateChannel);
        await _dbHelper.Context.SaveChangesAsync();
        
        SetupUser(1, "testuser1");

        // Act
        var result = await _controller.GetMessages(privateChannel.Id);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetMessages_ForPrivateChannel_ReturnsMessagesIfMember()
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

        // Add a message
        _dbHelper.Context.Messages.Add(new Message
        {
            ChannelId = privateChannel.Id,
            UserId = 1,
            Content = "Private message",
            Timestamp = DateTime.UtcNow
        });
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMessages(privateChannel.Id);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = okResult.Value.Should().BeAssignableTo<IEnumerable<MessageDto>>().Subject;
        
        messages.Should().Contain(m => m.Content == "Private message");
    }

    [Fact]
    public async Task GetMessages_RespectsMaxMessagesLimit()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Add more messages than the limit
        for (int i = 0; i < 60; i++)
        {
            _dbHelper.Context.Messages.Add(new Message
            {
                ChannelId = 1,
                UserId = 1,
                Content = $"Message {i}",
                Timestamp = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await _dbHelper.Context.SaveChangesAsync();

        // Act
        var result = await _controller.GetMessages(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = okResult.Value.Should().BeAssignableTo<IEnumerable<MessageDto>>().Subject;
        
        messages.Should().HaveCount(50); // Max limit
    }

    [Fact]
    public async Task GetMessages_NonWorkspaceMember_ReturnsForbidden()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(2, "testuser2"); // User 2 is not a workspace member

        // Act
        var result = await _controller.GetMessages(1);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    #endregion
}

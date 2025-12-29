using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Moq;
using SharpTalk.Api.Controllers;
using SharpTalk.Api.Hubs;
using SharpTalk.Api.Services;
using SharpTalk.Api.Tests.Helpers;
using SharpTalk.Shared.DTOs;
using StackExchange.Redis;

namespace SharpTalk.Api.Tests.Controllers;

public class UserControllerTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly UserController _controller;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<FileUploadService> _fileUploadServiceMock;
    private readonly Mock<IHubContext<ChatHub>> _hubContextMock;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDatabaseMock;

    public UserControllerTests()
    {
        _dbHelper = TestDbContextHelper.Create();
        
        // Create mocks
        _environmentMock = new Mock<IWebHostEnvironment>();
        _environmentMock.Setup(x => x.ContentRootPath).Returns(Path.GetTempPath());
        
        _configurationMock = new Mock<IConfiguration>();
        
        var fileUploadLoggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<FileUploadService>>();
        _fileUploadServiceMock = new Mock<FileUploadService>(
            _dbHelper.Context,
            _environmentMock.Object,
            _configurationMock.Object,
            fileUploadLoggerMock.Object);
        
        _hubContextMock = new Mock<IHubContext<ChatHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDatabaseMock = new Mock<IDatabase>();
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDatabaseMock.Object);
        
        // Setup Redis mock for online users
        _redisDatabaseMock
            .Setup(x => x.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(Array.Empty<RedisValue>());

        _controller = new UserController(
            _dbHelper.Context,
            _environmentMock.Object,
            _configurationMock.Object,
            _fileUploadServiceMock.Object,
            _hubContextMock.Object,
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

    #region GetCurrentUserProfile Tests

    [Fact]
    public async Task GetCurrentUserProfile_ReturnsCurrentUser()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Act
        var result = await _controller.GetCurrentUserProfile();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var user = okResult.Value.Should().BeOfType<UserInfo>().Subject;
        
        user.Username.Should().Be("testuser1");
        user.Email.Should().Be("test1@example.com");
    }

    [Fact]
    public async Task GetCurrentUserProfile_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(999, "nonexistent"); // Non-existent user

        // Act
        var result = await _controller.GetCurrentUserProfile();

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region UpdateUserProfile Tests

    [Fact]
    public async Task UpdateUserProfile_UpdatesUsernameAndEmail()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new UserInfo
        {
            Id = 1,
            Username = "updateduser",
            Email = "updated@example.com"
        };

        // Act
        var result = await _controller.UpdateUserProfile(request);

        // Assert
        result.Should().BeOfType<OkResult>();
        
        var user = _dbHelper.Context.Users.Find(1);
        user!.Username.Should().Be("updateduser");
        user.Email.Should().Be("updated@example.com");
    }

    [Fact]
    public async Task UpdateUserProfile_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new UserInfo
        {
            Id = 1,
            Username = "testuser1",
            Email = "test2@example.com" // Already exists (user 2)
        };

        // Act
        var result = await _controller.UpdateUserProfile(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateUserProfile_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        var request = new UserInfo
        {
            Id = 1,
            Username = "testuser2", // Already exists
            Email = "test1@example.com"
        };

        // Act
        var result = await _controller.UpdateUserProfile(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UpdateStatus Tests

    [Fact]
    public async Task UpdateStatus_UpdatesUserStatus()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupUser(1, "testuser1");

        // Setup Redis mock for status update
        _redisDatabaseMock
            .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), null, false, When.Always, CommandFlags.None))
            .ReturnsAsync(true);
        
        _redisDatabaseMock
            .Setup(x => x.SetContainsAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var request = new UpdateStatusRequest { Status = "Away" };

        // Act
        var result = await _controller.UpdateStatus(request);

        // Assert
        result.Should().BeOfType<OkResult>();
        
        var user = _dbHelper.Context.Users.Find(1);
        user!.Status.Should().Be("Away");
    }

    #endregion
}

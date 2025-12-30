using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SharpTalk.Api.Entities;
using SharpTalk.Api.Hubs;
using SharpTalk.Api.Tests.Helpers;
using StackExchange.Redis;

namespace SharpTalk.Api.Tests.Hubs;

public class ChatHubTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDatabaseMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _allClientsMock;
    private readonly Mock<IClientProxy> _groupClientsMock;
    private readonly Mock<IGroupManager> _groupsMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<ILogger<ChatHub>> _loggerMock;
    private readonly ChatHub _hub;

    public ChatHubTests()
    {
        _dbHelper = TestDbContextHelper.Create();

        // Setup Redis mock
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDatabaseMock = new Mock<IDatabase>();
        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_redisDatabaseMock.Object);

        // Setup SignalR mocks
        _clientsMock = new Mock<IHubCallerClients>();
        _allClientsMock = new Mock<IClientProxy>();
        _groupClientsMock = new Mock<IClientProxy>();
        _groupsMock = new Mock<IGroupManager>();
        _contextMock = new Mock<HubCallerContext>();
        _loggerMock = new Mock<ILogger<ChatHub>>();

        _clientsMock.Setup(c => c.All).Returns(_allClientsMock.Object);
        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupClientsMock.Object);

        _hub = new ChatHub(_dbHelper.Context, _redisMock.Object, _loggerMock.Object)
        {
            Clients = _clientsMock.Object,
            Groups = _groupsMock.Object,
            Context = _contextMock.Object
        };
    }

    private void SetupHubUser(int userId, string username, string connectionId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _contextMock.Setup(c => c.User).Returns(principal);
        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId);
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
    }

    #region OnConnectedAsync Tests

    [Fact]
    public async Task OnConnectedAsync_AddsUserToOnlineSet()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupHubUser(1, "testuser1", "conn1");

        _redisDatabaseMock
            .Setup(x => x.SetAddAsync(It.Is<RedisKey>(k => k == "online_users"), It.Is<RedisValue>(v => (int)v == 1), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _redisDatabaseMock
            .Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _redisDatabaseMock.Verify(
            x => x.SetAddAsync(It.Is<RedisKey>(k => k == "online_users"), It.Is<RedisValue>(v => (int)v == 1), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    #endregion

    #region OnDisconnectedAsync Tests

    [Fact]
    public async Task OnDisconnectedAsync_RemovesUserFromOnlineSet()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupHubUser(1, "testuser1", "conn1");

        _redisDatabaseMock
            .Setup(x => x.SetRemoveAsync(It.Is<RedisKey>(k => k == "online_users"), It.Is<RedisValue>(v => (int)v == 1), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _redisDatabaseMock
            .Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _redisDatabaseMock.Verify(
            x => x.SetRemoveAsync(It.Is<RedisKey>(k => k == "online_users"), It.Is<RedisValue>(v => (int)v == 1), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    #endregion

    #region JoinChannel Tests

    [Fact]
    public async Task JoinChannel_AddsToSignalRGroup()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupHubUser(1, "testuser1", "conn1");

        _groupsMock
            .Setup(g => g.AddToGroupAsync("conn1", "1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinChannel(1);

        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync("conn1", "1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region LeaveChannel Tests

    [Fact]
    public async Task LeaveChannel_RemovesFromSignalRGroup()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupHubUser(1, "testuser1", "conn1");

        _groupsMock
            .Setup(g => g.RemoveFromGroupAsync("conn1", "1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.LeaveChannel(1);

        // Assert
        _groupsMock.Verify(
            g => g.RemoveFromGroupAsync("conn1", "1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SendMessage Tests

    [Fact]
    public async Task SendMessage_PersistsMessage()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupHubUser(1, "testuser1", "conn1");

        var initialMessageCount = _dbHelper.Context.Messages.Count();

        // Act
        await _hub.SendMessage(1, "Test message content", null);

        // Assert
        var newMessageCount = _dbHelper.Context.Messages.Count();
        newMessageCount.Should().Be(initialMessageCount + 1);

        var message = _dbHelper.Context.Messages.OrderByDescending(m => m.Id).First();
        message.Content.Should().Be("Test message content");
    }

    [Fact]
    public async Task SendMessage_BroadcastsToChannel()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupHubUser(1, "testuser1", "conn1");

        // Act
        await _hub.SendMessage(1, "Test message", null);

        // Assert - Hub uses channel ID as group name
        _clientsMock.Verify(c => c.Group("1"), Times.AtLeastOnce);
    }

    #endregion

    #region JoinWorkspace Tests

    [Fact]
    public async Task JoinWorkspace_AddsToSignalRGroup()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        SetupHubUser(1, "testuser1", "conn1");

        _groupsMock
            .Setup(g => g.AddToGroupAsync("conn1", "workspace_1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _hub.JoinWorkspace(1);

        // Assert
        _groupsMock.Verify(
            g => g.AddToGroupAsync("conn1", "workspace_1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

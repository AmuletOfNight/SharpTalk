using Blazored.LocalStorage;
using FluentAssertions;
using Moq;
using Moq.Protected;
using SharpTalk.Shared.DTOs;
using SharpTalk.Web.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SharpTalk.Web.Tests.Services;

public class WorkspaceServiceTests
{
    private readonly Mock<ILocalStorageService> _localStorageMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
    {
        _localStorageMock = new Mock<ILocalStorageService>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5298/")
        };
        
        // Setup token retrieval
        _localStorageMock
            .Setup(x => x.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-jwt-token");
        
        _service = new WorkspaceService(_httpClient, _localStorageMock.Object);
    }

    private void SetupHttpResponse<T>(HttpMethod method, string endpoint, T responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == method && 
                    req.RequestUri!.PathAndQuery.Contains(endpoint)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(JsonSerializer.Serialize(responseContent), System.Text.Encoding.UTF8, "application/json")
            });
    }

    private void SetupEmptyHttpResponse(HttpMethod method, string endpoint, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == method && 
                    req.RequestUri!.PathAndQuery.Contains(endpoint)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode
            });
    }

    #region GetMyWorkspacesAsync Tests

    [Fact]
    public async Task GetMyWorkspacesAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var workspaces = new List<WorkspaceDto>
        {
            new() { Id = 1, Name = "Test Workspace", OwnerId = 1 }
        };
        SetupHttpResponse(HttpMethod.Get, "api/workspace", workspaces);

        // Act
        var result = await _service.GetMyWorkspacesAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Test Workspace");
        
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri!.PathAndQuery == "/api/workspace"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetMyWorkspacesAsync_ReturnsEmptyListOnNoWorkspaces()
    {
        // Arrange
        SetupHttpResponse(HttpMethod.Get, "api/workspace", new List<WorkspaceDto>());

        // Act
        var result = await _service.GetMyWorkspacesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region CreateWorkspaceAsync Tests

    [Fact]
    public async Task CreateWorkspaceAsync_PostsToCorrectEndpoint()
    {
        // Arrange
        var request = new CreateWorkspaceRequest { Name = "New Workspace" };
        var expectedResponse = new WorkspaceDto { Id = 1, Name = "New Workspace", OwnerId = 1 };
        SetupHttpResponse(HttpMethod.Post, "api/workspace", expectedResponse);

        // Act
        var result = await _service.CreateWorkspaceAsync(request);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Workspace");
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ReturnsNullOnFailure()
    {
        // Arrange
        var request = new CreateWorkspaceRequest { Name = "New Workspace" };
        SetupEmptyHttpResponse(HttpMethod.Post, "api/workspace", HttpStatusCode.BadRequest);

        // Act
        var result = await _service.CreateWorkspaceAsync(request);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region InviteUserAsync Tests

    [Fact]
    public async Task InviteUserAsync_PostsCorrectPayload()
    {
        // Arrange
        var request = new InviteUserRequest { WorkspaceId = 1, Username = "testuser" };
        SetupEmptyHttpResponse(HttpMethod.Post, "api/workspace/invite", HttpStatusCode.OK);

        // Act
        var result = await _service.InviteUserAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InviteUserAsync_ReturnsFalseOnFailure()
    {
        // Arrange
        var request = new InviteUserRequest { WorkspaceId = 1, Username = "nonexistent" };
        SetupEmptyHttpResponse(HttpMethod.Post, "api/workspace/invite", HttpStatusCode.BadRequest);

        // Act
        var result = await _service.InviteUserAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetWorkspaceMembersAsync Tests

    [Fact]
    public async Task GetWorkspaceMembersAsync_ReturnsMembers()
    {
        // Arrange
        var members = new List<UserStatusDto>
        {
            new() { UserId = 1, Username = "testuser1", Status = "Online" }
        };
        SetupHttpResponse(HttpMethod.Get, "api/workspace/1/members", members);

        // Act
        var result = await _service.GetWorkspaceMembersAsync(1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Username.Should().Be("testuser1");
    }

    #endregion

    #region RenameWorkspaceAsync Tests

    [Fact]
    public async Task RenameWorkspaceAsync_PutsToCorrectEndpoint()
    {
        // Arrange
        SetupEmptyHttpResponse(HttpMethod.Put, "api/workspace/1/rename", HttpStatusCode.OK);

        // Act
        var result = await _service.RenameWorkspaceAsync(1, "Renamed Workspace");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region DeleteWorkspaceAsync Tests

    [Fact]
    public async Task DeleteWorkspaceAsync_DeletesWorkspace()
    {
        // Arrange
        SetupEmptyHttpResponse(HttpMethod.Delete, "api/workspace/1", HttpStatusCode.OK);

        // Act
        var result = await _service.DeleteWorkspaceAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region LeaveWorkspaceAsync Tests

    [Fact]
    public async Task LeaveWorkspaceAsync_LeavesWorkspace()
    {
        // Arrange
        SetupEmptyHttpResponse(HttpMethod.Delete, "api/workspace/1/leave", HttpStatusCode.OK);

        // Act
        var result = await _service.LeaveWorkspaceAsync(1);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}

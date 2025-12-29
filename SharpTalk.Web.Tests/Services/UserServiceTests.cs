using Blazored.LocalStorage;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using SharpTalk.Shared.DTOs;
using SharpTalk.Web.Services;
using System.Net;
using System.Text.Json;

namespace SharpTalk.Web.Tests.Services;

public class UserServiceTests
{
    private readonly Mock<ILocalStorageService> _localStorageMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;
    private readonly Mock<AuthenticationStateProvider> _authStateProviderMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _localStorageMock = new Mock<ILocalStorageService>();
        _navigationManagerMock = new Mock<NavigationManager>();
        _authStateProviderMock = new Mock<AuthenticationStateProvider>();
        _configurationMock = new Mock<IConfiguration>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5298/")
        };
        
        // Setup token retrieval
        _localStorageMock
            .Setup(x => x.GetItemAsync<string>("authToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-jwt-token");
        
        _service = new UserService(
            _httpClient, 
            _localStorageMock.Object, 
            _navigationManagerMock.Object,
            _authStateProviderMock.Object,
            _configurationMock.Object);
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

    #region GetCurrentUserAsync Tests

    [Fact]
    public async Task GetCurrentUserAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var user = new UserInfo { Id = 1, Username = "testuser", Email = "test@example.com" };
        SetupHttpResponse(HttpMethod.Get, "api/user/me", user);

        // Act
        var result = await _service.GetCurrentUserAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task GetCurrentUserAsync_ReturnsNullOnUnauthorized()
    {
        // Arrange
        SetupEmptyHttpResponse(HttpMethod.Get, "api/user/me", HttpStatusCode.Unauthorized);

        // Act
        var result = await _service.GetCurrentUserAsync();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateUserInfoAsync Tests

    [Fact]
    public async Task UpdateUserInfoAsync_PutsToCorrectEndpoint()
    {
        // Arrange
        var request = new UserInfo { Id = 1, Username = "newusername", Email = "new@example.com" };
        SetupEmptyHttpResponse(HttpMethod.Put, "api/user/profile", HttpStatusCode.OK);

        // Act
        var result = await _service.UpdateUserInfoAsync(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUserInfoAsync_ReturnsFalseOnConflict()
    {
        // Arrange
        var request = new UserInfo { Id = 1, Username = "existinguser", Email = "test@example.com" };
        SetupEmptyHttpResponse(HttpMethod.Put, "api/user/profile", HttpStatusCode.BadRequest);

        // Act
        var result = await _service.UpdateUserInfoAsync(request);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region UpdateStatusAsync Tests

    [Fact]
    public async Task UpdateStatusAsync_PostsCorrectly()
    {
        // Arrange
        SetupEmptyHttpResponse(HttpMethod.Post, "api/user/status", HttpStatusCode.OK);

        // Act
        var result = await _service.UpdateStatusAsync("Away");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region LogoutAsync Tests

    [Fact]
    public async Task LogoutAsync_ClearsLocalStorage()
    {
        // Arrange
        _localStorageMock
            .Setup(x => x.RemoveItemAsync("authToken", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _localStorageMock
            .Setup(x => x.RemoveItemAsync("userInfo", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _localStorageMock
            .Setup(x => x.RemoveItemAsync("selectedWorkspaceId", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        _localStorageMock
            .Setup(x => x.RemoveItemAsync("selectedChannelId", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _service.LogoutAsync();

        // Assert
        _localStorageMock.Verify(
            x => x.RemoveItemAsync("authToken", It.IsAny<CancellationToken>()), 
            Times.Once);
        _localStorageMock.Verify(
            x => x.RemoveItemAsync("userInfo", It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    #endregion
}

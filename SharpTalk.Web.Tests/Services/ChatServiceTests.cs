using Blazored.LocalStorage;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using SharpTalk.Shared.DTOs;
using SharpTalk.Web.Services;
using System.Net;
using System.Text.Json;

namespace SharpTalk.Web.Tests.Services;

public class ChatServiceTests
{
    private readonly Mock<ILocalStorageService> _localStorageMock;
    private readonly Mock<NavigationManager> _navigationManagerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly ChatService _service;

    public ChatServiceTests()
    {
        _localStorageMock = new Mock<ILocalStorageService>();
        _navigationManagerMock = new Mock<NavigationManager>();
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
        
        // Setup SignalR hub URL
        _configurationMock.Setup(x => x["ApiSettings:SignalRHubUrl"]).Returns("http://localhost:5298/chatHub");
        _configurationMock.Setup(x => x["FileUploadSettings:MaxChatFileSizeBytes"]).Returns("10485760");
        
        _service = new ChatService(
            _httpClient, 
            _navigationManagerMock.Object, 
            _localStorageMock.Object,
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

    #region GetMessageHistoryAsync Tests

    [Fact]
    public async Task GetMessageHistoryAsync_CallsCorrectEndpoint()
    {
        // Arrange
        var messages = new List<MessageDto>
        {
            new() { Id = 1, Content = "Test message", ChannelId = 1, UserId = 1 }
        };
        SetupHttpResponse(HttpMethod.Get, "api/message/1", messages);

        // Act
        var result = await _service.GetMessageHistoryAsync(1);

        // Assert
        result.Should().HaveCount(1);
        result[0].Content.Should().Be("Test message");
    }

    [Fact]
    public async Task GetMessageHistoryAsync_ReturnsEmptyListOnNoMessages()
    {
        // Arrange
        SetupHttpResponse(HttpMethod.Get, "api/message/1", new List<MessageDto>());

        // Act
        var result = await _service.GetMessageHistoryAsync(1);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UploadFilesAsync Tests

    [Fact]
    public async Task UploadFilesAsync_PostsMultipartContent()
    {
        // Arrange
        var expectedAttachments = new List<AttachmentDto>
        {
            new() { Id = 1, FileName = "test.txt", FileType = "text/plain" }
        };
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Post && 
                    req.RequestUri!.PathAndQuery.Contains("api/message/upload") &&
                    req.Content is MultipartFormDataContent),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedAttachments), System.Text.Encoding.UTF8, "application/json")
            });

        // Create mock browser file
        var fileMock = new Mock<Microsoft.AspNetCore.Components.Forms.IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns("test.txt");
        fileMock.Setup(f => f.ContentType).Returns("text/plain");
        fileMock.Setup(f => f.Size).Returns(100);
        fileMock.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(new MemoryStream("Test content"u8.ToArray()));

        // Act
        var result = await _service.UploadFilesAsync(new List<Microsoft.AspNetCore.Components.Forms.IBrowserFile> { fileMock.Object }, 1);

        // Assert
        result.Should().HaveCount(1);
        result[0].FileName.Should().Be("test.txt");
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_DisposesHubConnection()
    {
        // Act & Assert - Should not throw
        await _service.DisposeAsync();
    }

    #endregion
}

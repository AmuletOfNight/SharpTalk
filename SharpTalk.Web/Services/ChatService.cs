using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.SignalR.Client;
using SharpTalk.Shared.DTOs;
using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace SharpTalk.Web.Services;

public class ChatService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private readonly ILocalStorageService _localStorage;
    private readonly IConfiguration _configuration;

    public event Action<MessageDto>? OnMessageReceived;
    public event Action<UserStatusDto>? OnUserStatusChanged;
    public event Action<int, int, string, bool>? OnUserTyping;

    public ChatService(HttpClient httpClient, NavigationManager navigationManager, ILocalStorageService localStorage, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
        _localStorage = localStorage;
        _configuration = configuration;
    }

    public async Task InitializeAsync()
    {
        if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected) return;

        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrEmpty(token)) return;

        if (_hubConnection == null)
        {
            var signalRHubUrl = _configuration["ApiSettings:SignalRHubUrl"] ?? "http://localhost:5298/chatHub";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(signalRHubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        return await _localStorage.GetItemAsync<string>("authToken");
                    };
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<MessageDto>("ReceiveMessage", (message) =>
            {
                OnMessageReceived?.Invoke(message);
            });

            _hubConnection.On<UserStatusDto>("UserStatusChanged", (status) =>
            {
                OnUserStatusChanged?.Invoke(status);
            });

            _hubConnection.On<int, int, string, bool>("UserTyping", (channelId, userId, username, isTyping) =>
            {
                OnUserTyping?.Invoke(channelId, userId, username, isTyping);
            });
        }

        try
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
            }
        }
        catch
        {
            // Connection error - will retry on next initialize
        }
    }

    public async Task JoinWorkspaceAsync(int workspaceId)
    {
        if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("JoinWorkspace", workspaceId);
        }
    }

    public async Task JoinChannelAsync(int channelId)
    {
        if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("JoinChannel", channelId);
        }
    }

    public async Task LeaveChannelAsync(int channelId)
    {
        if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("LeaveChannel", channelId);
        }
    }

    public async Task SendMessageAsync(int channelId, string content, List<int> attachmentIds = null)
    {
        if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("SendMessage", channelId, content, attachmentIds);
        }
    }

    public async Task<List<AttachmentDto>> UploadFilesAsync(List<IBrowserFile> files, int channelId)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);



        var content = new MultipartFormDataContent();
        var maxFileSize = int.Parse(_configuration["FileUploadSettings:MaxChatFileSizeBytes"] ?? "10485760");

        foreach (var file in files)
        {


            var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: maxFileSize));

            // Handle empty or null ContentType by using a default MIME type
            var contentType = string.IsNullOrEmpty(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "files", file.Name);
        }

        content.Add(new StringContent(channelId.ToString()), "channelId");

        var response = await _httpClient.PostAsync("api/message/upload", content);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<AttachmentDto>>() ?? new List<AttachmentDto>();
        }

        var errorMessage = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"Upload failed with status {response.StatusCode}: {errorMessage}");
    }

    public async Task<string> DownloadAttachmentAsync(int attachmentId)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.GetAsync($"api/message/attachment/{attachmentId}");
        if (response.IsSuccessStatusCode)
        {
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "download";

            // Create download link
            return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
        }

        return null;
    }

    public async Task SendTypingIndicatorAsync(int channelId, bool isTyping)
    {
        if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("SendTypingIndicator", channelId, isTyping);
        }
    }

    public async Task<List<MessageDto>> GetMessageHistoryAsync(int channelId)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return await _httpClient.GetFromJsonAsync<List<MessageDto>>($"api/message/{channelId}") ?? new List<MessageDto>();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

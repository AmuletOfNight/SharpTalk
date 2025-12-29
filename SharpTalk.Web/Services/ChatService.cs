using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SharpTalk.Shared.DTOs;
using System.Net.Http.Json;

namespace SharpTalk.Web.Services;

public class ChatService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private readonly ILocalStorageService _localStorage;

    public event Action<MessageDto>? OnMessageReceived;
    public event Action<UserStatusDto>? OnUserStatusChanged;
    public event Action<int, int, string, bool>? OnUserTyping;

    public ChatService(HttpClient httpClient, NavigationManager navigationManager, ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _navigationManager = navigationManager;
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5298/chatHub", options =>
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

        await _hubConnection.StartAsync();
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

    public async Task SendMessageAsync(int channelId, string content)
    {
        if (_hubConnection is not null && _hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.SendAsync("SendMessage", channelId, content);
        }
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

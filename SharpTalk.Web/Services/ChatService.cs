using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SharpTalk.Shared.DTOs;

namespace SharpTalk.Web.Services;

public class ChatService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;
    private readonly ILocalStorageService _localStorage;

    public event Action<MessageDto>? OnMessageReceived;

    public ChatService(NavigationManager navigationManager, ILocalStorageService localStorage)
    {
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

        await _hubConnection.StartAsync();
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

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

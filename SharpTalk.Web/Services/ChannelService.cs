using Blazored.LocalStorage;
using SharpTalk.Shared.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SharpTalk.Web.Services;

public class ChannelService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;

    public ChannelService(HttpClient httpClient, ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
    }

    private async Task SetAuthHeader()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<List<ChannelDto>> GetChannelsAsync(int workspaceId)
    {
        await SetAuthHeader();
        return await _httpClient.GetFromJsonAsync<List<ChannelDto>>($"api/channel/{workspaceId}") ?? new List<ChannelDto>();
    }

    public async Task<ChannelDto?> CreateChannelAsync(CreateChannelRequest request)
    {
        await SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("api/channel", request);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ChannelDto>();
        }

        return null;
    }

    public async Task<ChannelDto?> StartDirectMessageAsync(int? workspaceId, int targetUserId)
    {
        await SetAuthHeader();
        var request = new CreateDirectMessageRequest { WorkspaceId = workspaceId, TargetUserId = targetUserId };
        var response = await _httpClient.PostAsJsonAsync("api/channel/dm", request);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ChannelDto>();
        }

        return null;
    }

    public async Task<List<ChannelDto>> GetDirectMessagesAsync()
    {
        await SetAuthHeader();
        return await _httpClient.GetFromJsonAsync<List<ChannelDto>>("api/channel/dms") ?? new List<ChannelDto>();
    }

    public async Task<ChannelDto?> CheckExistingGlobalDMAsync(int targetUserId)
    {
        await SetAuthHeader();
        var response = await _httpClient.GetAsync($"api/channel/dm/check/{targetUserId}");
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ChannelDto?>();
        }
        return null;
    }
}

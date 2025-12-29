using Blazored.LocalStorage;
using SharpTalk.Shared.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SharpTalk.Web.Services;

public class WorkspaceService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;

    public WorkspaceService(HttpClient httpClient, ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
    }

    public async Task<List<WorkspaceDto>> GetMyWorkspacesAsync()
    {
        await SetAuthHeader();
        return await _httpClient.GetFromJsonAsync<List<WorkspaceDto>>("api/workspace") ?? new List<WorkspaceDto>();
    }

    public async Task<WorkspaceDto?> CreateWorkspaceAsync(CreateWorkspaceRequest request)
    {
        await SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("api/workspace", request);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<WorkspaceDto>();
        }

        return null;
    }

    private async Task SetAuthHeader()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
    public async Task<bool> InviteUserAsync(InviteUserRequest request)
    {
        await SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("api/workspace/invite", request);
        return response.IsSuccessStatusCode;
    }
}

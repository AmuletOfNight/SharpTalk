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

    public async Task<WorkspaceDto?> GetWorkspaceByIdAsync(int workspaceId)
    {
        await SetAuthHeader();
        return await _httpClient.GetFromJsonAsync<WorkspaceDto>($"api/workspace/{workspaceId}");
    }

    public async Task<bool> ReorderWorkspacesAsync(List<int> workspaceIds)
    {
        await SetAuthHeader();
        var response = await _httpClient.PostAsJsonAsync("api/workspace/reorder", workspaceIds);
        return response.IsSuccessStatusCode;
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

    public async Task<List<UserStatusDto>> GetWorkspaceMembersAsync(int workspaceId)
    {
        await SetAuthHeader();
        return await _httpClient.GetFromJsonAsync<List<UserStatusDto>>($"api/workspace/{workspaceId}/members") ?? new List<UserStatusDto>();
    }

    public async Task<List<ChannelDto>> GetWorkspaceChannelsAsync(int workspaceId)
    {
        await SetAuthHeader();
        return await _httpClient.GetFromJsonAsync<List<ChannelDto>>($"api/channel/{workspaceId}") ?? new List<ChannelDto>();
    }

    public async Task<List<WorkspaceMemberDto>> GetWorkspaceMembersDetailedAsync(int workspaceId)
    {
        await SetAuthHeader();
        return await _httpClient.GetFromJsonAsync<List<WorkspaceMemberDto>>($"api/workspace/{workspaceId}/members-detailed") ?? new List<WorkspaceMemberDto>();
    }

    public async Task<bool> RenameWorkspaceAsync(int workspaceId, string newName)
    {
        await SetAuthHeader();
        var request = new RenameWorkspaceRequest { WorkspaceId = workspaceId, NewName = newName };
        var response = await _httpClient.PutAsJsonAsync($"api/workspace/{workspaceId}/rename", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateWorkspaceDescriptionAsync(int workspaceId, string description)
    {
        await SetAuthHeader();
        var request = new UpdateWorkspaceDescriptionRequest { WorkspaceId = workspaceId, Description = description };
        var response = await _httpClient.PutAsJsonAsync($"api/workspace/{workspaceId}/description", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveMemberAsync(int workspaceId, int userId)
    {
        await SetAuthHeader();
        var response = await _httpClient.DeleteAsync($"api/workspace/{workspaceId}/members/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> LeaveWorkspaceAsync(int workspaceId)
    {
        await SetAuthHeader();
        var response = await _httpClient.DeleteAsync($"api/workspace/{workspaceId}/leave");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteWorkspaceAsync(int workspaceId)
    {
        await SetAuthHeader();
        var response = await _httpClient.DeleteAsync($"api/workspace/{workspaceId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TransferOwnershipAsync(int workspaceId, int newOwnerId)
    {
        await SetAuthHeader();
        var request = new TransferOwnershipRequest { WorkspaceId = workspaceId, NewOwnerId = newOwnerId };
        var response = await _httpClient.PostAsJsonAsync($"api/workspace/{workspaceId}/transfer-ownership", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateMemberRoleAsync(int workspaceId, int userId, string newRole)
    {
        await SetAuthHeader();
        var request = new UpdateMemberRoleRequest { WorkspaceId = workspaceId, UserId = userId, NewRole = newRole };
        var response = await _httpClient.PutAsJsonAsync($"api/workspace/{workspaceId}/members/{userId}/role", request);
        return response.IsSuccessStatusCode;
    }
}

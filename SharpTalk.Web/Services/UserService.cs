using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using SharpTalk.Shared.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SharpTalk.Web.Services;

public class UserService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigationManager;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly IConfiguration _configuration;

    public event Action<UserInfo>? OnUserInfoChanged;

    private UserInfo? _currentUser;
    public UserInfo? CurrentUser => _currentUser;

    public UserService(HttpClient httpClient, ILocalStorageService localStorage,
        NavigationManager navigationManager, AuthenticationStateProvider authStateProvider,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _navigationManager = navigationManager;
        _authStateProvider = authStateProvider;
        _configuration = configuration;
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var userInfoJson = await _localStorage.GetItemAsync<string>("userInfo");
        if (!string.IsNullOrEmpty(userInfoJson))
        {
            var cachedUser = System.Text.Json.JsonSerializer.Deserialize<UserInfo>(userInfoJson);
            if (cachedUser != null)
            {
                _currentUser = cachedUser;
                return cachedUser;
            }
        }

        try
        {
            var user = await _httpClient.GetFromJsonAsync<UserInfo>("api/user/profile");
            if (user != null)
            {
                await _localStorage.SetItemAsync("userInfo", System.Text.Json.JsonSerializer.Serialize(user));
                _currentUser = user;
                OnUserInfoChanged?.Invoke(user);
            }
            return user;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateUserInfoAsync(UserInfo userInfo)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/user/profile", userInfo);
            if (response.IsSuccessStatusCode)
            {
                var updatedUser = await response.Content.ReadFromJsonAsync<UserInfo>();
                if (updatedUser != null)
                {
                    await _localStorage.SetItemAsync("userInfo", System.Text.Json.JsonSerializer.Serialize(updatedUser));
                    _currentUser = updatedUser;
                    OnUserInfoChanged?.Invoke(updatedUser);
                    return true;
                }
            }
        }
        catch
        {
            // Handle error
        }

        return false;
    }

    public async Task<bool> UploadAvatarAsync(IBrowserFile file)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var content = new MultipartFormDataContent();
            var maxAvatarFileSize = int.Parse(_configuration["FileUploadSettings:MaxAvatarFileSizeBytes"] ?? "2097152");
            var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: maxAvatarFileSize));

            // Handle empty or null ContentType by using a default MIME type
            var contentType = string.IsNullOrEmpty(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "avatar", file.Name);

            var response = await _httpClient.PostAsync("api/user/avatar", content);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AvatarUploadResult>();
                if (result != null && _currentUser != null)
                {
                    _currentUser.AvatarUrl = result.AvatarUrl;
                    await _localStorage.SetItemAsync("userInfo", System.Text.Json.JsonSerializer.Serialize(_currentUser));
                    OnUserInfoChanged?.Invoke(_currentUser);
                    return true;
                }
            }
        }
        catch
        {
            // Handle error
        }

        return false;
    }

    public async Task<bool> UpdateStatusAsync(string status)
    {
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await _httpClient.PutAsJsonAsync("api/user/status", new { Status = status });
            if (response.IsSuccessStatusCode && _currentUser != null)
            {
                _currentUser.Status = status;
                await _localStorage.SetItemAsync("userInfo", System.Text.Json.JsonSerializer.Serialize(_currentUser));
                OnUserInfoChanged?.Invoke(_currentUser);
                return true;
            }
        }
        catch
        {
            // Handle error
        }

        return false;
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("userInfo");
        _currentUser = null;

        if (_authStateProvider is SharpTalk.Web.Auth.CustomAuthStateProvider customProvider)
        {
            customProvider.MarkUserAsLoggedOut();
        }

        _navigationManager.NavigateTo("/login");
    }

    private class AvatarUploadResult
    {
        public string AvatarUrl { get; set; } = string.Empty;
    }
}

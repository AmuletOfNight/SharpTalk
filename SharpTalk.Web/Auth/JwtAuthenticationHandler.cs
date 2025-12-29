using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;
using System.Net.Http.Headers;

namespace SharpTalk.Web.Auth;

public class JwtAuthenticationHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly NavigationManager _navigationManager;
    private readonly IServiceProvider _serviceProvider;

    public JwtAuthenticationHandler(
        ILocalStorageService localStorage,
        NavigationManager navigationManager,
        IServiceProvider serviceProvider)
    {
        _localStorage = localStorage;
        _navigationManager = navigationManager;
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Add the authorization header if the token exists
        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // Check for 401 Unauthorized or 404 Not Found on the profile endpoint
        if (response.StatusCode == HttpStatusCode.Unauthorized ||
            (response.StatusCode == HttpStatusCode.NotFound && request.RequestUri?.AbsolutePath.EndsWith("api/user/profile") == true))
        {
            await HandleAuthFailure();
        }

        return response;
    }

    private async Task HandleAuthFailure()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("userInfo");

        // Resolve AuthenticationStateProvider manually to handle potential circular dependencies if any arise,
        // though with the refactor they should be minimized.
        // We use a scope here or just the provider since we are in a Blazor WASM app which is effectively single scope.
        if (_serviceProvider.GetService(typeof(AuthenticationStateProvider)) is CustomAuthStateProvider authStateProvider)
        {
            authStateProvider.MarkUserAsLoggedOut();
        }

        _navigationManager.NavigateTo("/login");
    }
}

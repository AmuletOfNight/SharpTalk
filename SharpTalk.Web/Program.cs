using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SharpTalk.Web;
using SharpTalk.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Load configuration
var configuration = builder.Configuration;
var apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5298";
var signalRHubUrl = configuration["ApiSettings:SignalRHubUrl"] ?? "http://localhost:5298/chatHub";

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<SharpTalk.Web.Services.WorkspaceService>();
builder.Services.AddScoped<SharpTalk.Web.Services.ChannelService>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<SharpTalk.Web.Services.ChatService>();
builder.Services.AddScoped<SharpTalk.Web.Services.UserService>();
builder.Services.AddScoped<SharpTalk.Web.Services.UrlUtilityService>();

await builder.Build().RunAsync();

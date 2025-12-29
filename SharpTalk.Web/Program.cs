using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SharpTalk.Web;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using SharpTalk.Web.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<SharpTalk.Web.Services.WorkspaceService>();
builder.Services.AddScoped<SharpTalk.Web.Services.ChannelService>();
builder.Services.AddScoped<SharpTalk.Web.Services.ChatService>();
builder.Services.AddScoped<SharpTalk.Web.Services.UserService>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5298") });

await builder.Build().RunAsync();

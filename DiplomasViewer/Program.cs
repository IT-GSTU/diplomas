using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DiplomasViewer;
using DiplomasViewer.Models;
using DiplomasViewer.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices();

// Настройки репозитория-хранилища из wwwroot/appsettings.json (секция "GitHub").
var gh = new GitHubOptions
{
    Owner = builder.Configuration["GitHub:Owner"] ?? "IT-GSTU",
    Repo = builder.Configuration["GitHub:Repo"] ?? "diplomas",
    Branch = builder.Configuration["GitHub:Branch"] ?? "main",
    DataPath = builder.Configuration["GitHub:DataPath"] ?? "data/diplomas.json",
};
builder.Services.AddSingleton(gh);
builder.Services.AddScoped<GitHubClient>();
builder.Services.AddScoped<AdminState>();
builder.Services.AddScoped<DiplomaService>();

await builder.Build().RunAsync();

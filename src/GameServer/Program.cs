using KitchenOrchestrator.GameServer.Hubs;
using KitchenOrchestrator.GameServer.Services;
using KitchenOrchestrator.Shared.Security.Jwt;
using KitchenOrchestrator.GameServer.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configuration Options
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<ServerAuthOptions>(builder.Configuration.GetSection("ServerAuth"));
builder.Services.Configure<GameServerOptions>(builder.Configuration.GetSection("GameServer"));

// Core Services
// AddSignalR automatically registers IHubContext<T> for all hubs � GameLoopService uses IHubContext<GameHub>
builder.Services.AddSignalR();

// Shared Security
builder.Services.AddSingleton<IJwtValidationService, JwtValidationService>();

// Game Server Logic
builder.Services.AddSingleton<IMatchSessionService, MatchSessionService>();
builder.Services.AddSingleton<IMatchSimulationService, MatchSimulationService>();

// HttpClient-managed outbound pipeline for match result submission
builder.Services.AddHttpClient<IMatchResultSubmissionService, MatchResultSubmissionService>();
builder.Services.AddSingleton<IMatchSimulationService, MatchSimulationService>();

// Background tick loop � IHubContext<GameHub> is injected automatically
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
app.MapHub<GameHub>("/gamehub");

app.Run();
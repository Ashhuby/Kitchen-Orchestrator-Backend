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
builder.Services.AddSignalR();

// Shared Security
builder.Services.AddSingleton<IJwtValidationService, JwtValidationService>();

// Game Server Logic
builder.Services.AddSingleton<IMatchSessionService, MatchSessionService>();

// Registered with HttpClient to manage the outgoing pipeline and connection pooling
builder.Services.AddHttpClient<IMatchResultSubmissionService, MatchResultSubmissionService>();

// The Heartbeat (Background Service)
builder.Services.AddHostedService<GameLoopService>();

var app = builder.Build();

// Endpoints
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// SignalR entry point
app.MapHub<GameHub>("/gamehub");

app.Run();
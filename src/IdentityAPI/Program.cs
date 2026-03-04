// Kitchen Orchestrator — IdentityAPI
// Bootstrapping placeholder. Full implementation added in Phase 1.
// This file exists so the project compiles cleanly during scaffolding.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

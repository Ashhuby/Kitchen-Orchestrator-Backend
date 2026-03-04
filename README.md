# Kitchen Orchestrator

A server-authoritative multiplayer co-op cooking game built for Steam.
Created to learn more and demonstrate my understaning of distributed systems, security, and scalable architecture.

## Stack

| Layer | Technology |
|---|---|
| Game Client | Godot 4.x (C#/.NET 8) + GodotSteam — Isometric 2D |
| Game Server | Headless Godot 4.x (C#) in Docker |
| Web API | ASP.NET Core 8 |
| Database | PostgreSQL via Supabase (EF Core 8 + Npgsql) |
| Infrastructure | Oracle Cloud A1 Flex ARM64, Docker, Nginx |
| Auth | Steam ticket verification → JWT |

## Architecture Principles

- **Server-authoritative:** All game logic runs on the headless server. The client renders only.
- **Steam-verified identity:** Tickets verified with Valve's partner API. Client-claimed identity is never trusted.
- **Clean dependency boundaries:** Shared code split into `Shared.Contracts`, `Shared.Security`, and `Shared.GameLogic` to prevent infrastructure dependencies leaking into the game server.

## Structure

```
src/
  Shared.Contracts/   # Pure POCOs, DTOs, Enums — zero dependencies
  Shared.Security/    # JWT utility — plain .NET 8, no ASP.NET
  Shared.GameLogic/   # Recipes, scoring, orders — pure computation
  IdentityAPI/        # ASP.NET Core — Steam auth, profiles, leaderboards
  GameServer/         # Headless Godot — authoritative simulation
  GameClient/         # Godot 4.x — isometric 2D rendering and input
tests/
  IdentityAPI.Tests/
  Shared.Tests/
```

See `docs/` for architecture and deployment documentation.

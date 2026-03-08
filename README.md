# Kitchen Orchestrator

A server-authoritative multiplayer co-op cooking game built for Steam.
Created to learn more and demonstrate my understanding of distributed systems, security, and scalable architecture.

## Stack

| Layer | Technology |
|---|---|
| Game Client Library | C# Class Library (.NET 8) for Godot integration |
| Game Server | ASP.NET Core 8 + SignalR (Hub-based simulation) |
| Web API | ASP.NET Core 8 |
| Database | PostgreSQL via Supabase (EF Core 8 + Npgsql) |
| Infrastructure | Oracle Cloud A1 Flex ARM64, Docker, Nginx |
| Auth | Steam ticket verification → JWT |

## Architecture Principles

- **Server-authoritative:** All game logic and match state are managed by the ASP.NET Core Game Server via SignalR.
- **Steam-verified identity:** Tickets verified with Valve's partner API. Client-claimed identity is never trusted.
- **Clean dependency boundaries:** Shared code split into `Shared.Contracts`, `Shared.Security`, and `Shared.GameLogic` to prevent infrastructure dependencies leaking into the game server or client.

## Structure

```
src/
  Shared.Contracts/   # Pure POCOs, DTOs, Enums — zero dependencies
  Shared.Security/    # JWT utility — plain .NET 8, no ASP.NET
  Shared.GameLogic/   # Recipes, scoring, orders — pure computation
  IdentityAPI/        # ASP.NET Core — Steam auth and player profiles
  GameServer/         # ASP.NET Core — SignalR match coordination
  GameClient/         # .NET 8 Library — Client-side Auth and Connection logic
infra/
  deploy.sh           # ARM64-targeted deployment script
  nginx.conf          # Reverse proxy and WebSocket gateway
```

## How to Run Locally

### Prerequisites
- .NET 8 SDK
- Docker Desktop

### Setup

1. Clone the repository:
```bash
git clone https://github.com/Ashhuby/Kitchen-Orchestrator-Backend.git
cd Kitchen-Orchestrator-Backend
```

2. Configure Environment: Copy `.env.example` to `.env` and fill in your Steam API keys and JWT secrets.

3. Launch Stack:
```bash
docker compose up --build
```

### Health Check URLs (via Nginx)

- Identity API: `http://localhost/health-identity`
- Game Server: `http://localhost/health-game`

## Deployment

The system is designed to run on an Oracle Cloud (OCI) A1.Flex ARM64 instance.

- **Automated Deployment:** Use the provided script to sync code, build images, and restart containers:
```bash
./infra/deploy.sh
```

- **Networking:** Nginx acts as a reverse proxy, routing all traffic on port 80. It handles standard REST requests for the IdentityAPI and manages WebSocket upgrades for the GameServer's SignalR hubs.

- **Firewall:** Only port 80 needs to be open in the OCI Security List.
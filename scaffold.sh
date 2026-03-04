#!/bin/bash
# ══════════════════════════════════════════════════════════════
# Kitchen Orchestrator — Repository Scaffold Script
# ══════════════════════════════════════════════════════════════
# Run this from the ROOT of your cloned repository:
#
#   git clone https://github.com/Ashhuby/Kitchen-Orchestrator-Backend.git
#   cd Kitchen-Orchestrator-Backend
#   chmod +x scaffold.sh
#   ./scaffold.sh
#   git add .
#   git commit -m "chore: initial project scaffold"
#   git push origin main
# ══════════════════════════════════════════════════════════════

set -e  # Exit immediately on any error

echo "Creating Kitchen Orchestrator folder structure..."

# ── Repo root level ────────────────────────────────────────
mkdir -p .github/workflows
mkdir -p docs
mkdir -p infra/nginx

# ── Shared.Contracts (zero dependencies — pure C# POCOs) ──
mkdir -p src/Shared.Contracts/Models
mkdir -p src/Shared.Contracts/DTOs
mkdir -p src/Shared.Contracts/Enums
mkdir -p src/Shared.Contracts/Interfaces

# ── Shared.Security (refs Contracts only — no ASP.NET) ────
mkdir -p src/Shared.Security/Jwt
mkdir -p src/Shared.Security/Hashing

# ── Shared.GameLogic (refs Contracts only — pure logic) ───
mkdir -p src/Shared.GameLogic/Recipes
mkdir -p src/Shared.GameLogic/Scoring
mkdir -p src/Shared.GameLogic/Orders
mkdir -p src/Shared.GameLogic/Levels

# ── IdentityAPI (ASP.NET Core 8) ──────────────────────────
mkdir -p src/IdentityAPI/Controllers
mkdir -p src/IdentityAPI/Services
mkdir -p src/IdentityAPI/Data/Configurations
mkdir -p src/IdentityAPI/Data/Migrations
mkdir -p src/IdentityAPI/Middleware
mkdir -p src/IdentityAPI/Configuration

# ── GameServer (Headless Godot C#) ────────────────────────
mkdir -p src/GameServer/Network
mkdir -p src/GameServer/Auth
mkdir -p src/GameServer/Simulation
mkdir -p src/GameServer/Reporting
mkdir -p src/GameServer/Configuration

# ── GameClient (Godot 4.x — Isometric 2D) ─────────────────
mkdir -p src/GameClient/addons
mkdir -p src/GameClient/Autoloads
mkdir -p src/GameClient/Scenes/UI
mkdir -p src/GameClient/Scenes/Kitchen
mkdir -p src/GameClient/Scenes/Lobby
mkdir -p src/GameClient/Scripts/Network
mkdir -p src/GameClient/Scripts/UI
mkdir -p src/GameClient/Scripts/Rendering/Isometric
mkdir -p src/GameClient/Assets/Sprites/Characters
mkdir -p src/GameClient/Assets/Sprites/Items
mkdir -p src/GameClient/Assets/Sprites/Stations
mkdir -p src/GameClient/Assets/Sprites/Tiles/Isometric
mkdir -p src/GameClient/Assets/Audio/SFX
mkdir -p src/GameClient/Assets/Audio/Music
mkdir -p src/GameClient/Assets/Fonts
mkdir -p src/GameClient/Assets/UI

# ── Tests ──────────────────────────────────────────────────
mkdir -p tests/IdentityAPI.Tests/Controllers
mkdir -p tests/IdentityAPI.Tests/Services
mkdir -p tests/Shared.Tests/Security
mkdir -p tests/Shared.Tests/GameLogic

# ══════════════════════════════════════════════════════════════
# Place .gitkeep in every empty directory
# Git does not track directories — only files.
# Without this, empty folders will not appear after git clone.
# ══════════════════════════════════════════════════════════════
find . -type d \
  -not -path './.git/*' \
  -not -path './.git' \
  | while read dir; do
    if [ -z "$(ls -A "$dir" 2>/dev/null)" ]; then
      touch "$dir/.gitkeep"
    fi
  done

# ══════════════════════════════════════════════════════════════
# Write root-level files
# ══════════════════════════════════════════════════════════════

# ── .gitignore ────────────────────────────────────────────
cat > .gitignore << 'GITIGNORE'
# ── .NET / C# ──────────────────────────────────────────────
[Oo]bj/
[Bb]in/
*.user
*.suo
.vs/
**/Properties/launchSettings.json
*.nupkg
*.snupkg
**/[Pp]ackages/
nuget.config
**/secrets.json
**/appsettings.Development.json

# ── Godot 4.x ──────────────────────────────────────────────
src/GameClient/.godot/
src/GameClient/.import/
**/*.import
src/GameClient/export/
src/GameClient/.mono/
src/GameClient/mono_crash.*.json

# ── Docker / Secrets ───────────────────────────────────────
.env
.env.local
.env.production
*.pem
*.key
*.p12
*.pfx
*.cer

# ── OS ─────────────────────────────────────────────────────
.DS_Store
Thumbs.db
desktop.ini

# ── IDE ────────────────────────────────────────────────────
.idea/
*.swp
.vscode/settings.json

# ── Test results ───────────────────────────────────────────
TestResults/
*.trx
coverage/
GITIGNORE

# ── .env.example ──────────────────────────────────────────
cat > .env.example << 'ENVEXAMPLE'
# Copy this to .env for local development. Never commit .env.
# Generate secrets with: openssl rand -base64 48

# JWT — minimum 32 characters
JWT__SIGNINGKEY=

# Steam publisher key — treat like a root password
STEAM__PUBLISHERKEY=
STEAM__APPID=480

# Game server <-> IdentityAPI shared secret
SERVERAUTH__SECRET=

# Supabase PgBouncer pooler URL (port 6543, NOT 5432)
CONNECTIONSTRINGS__DEFAULTCONNECTION=

# URL the GameServer uses to POST match results
IDENTITYAPI__BASEURL=https://your-domain.com
ENVEXAMPLE

# ── README.md ─────────────────────────────────────────────
cat > README.md << 'README'
# Kitchen Orchestrator

A server-authoritative multiplayer co-op cooking game built for Steam.
Backend engineering portfolio piece demonstrating distributed systems, security, and scalable architecture.

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
README

# ── CI workflow ───────────────────────────────────────────
cat > .github/workflows/ci.yml << 'CIYML'
name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore KitchenOrchestrator.sln

      - name: Build
        run: dotnet build KitchenOrchestrator.sln --no-restore -c Release

      - name: Test
        run: dotnet test KitchenOrchestrator.sln --no-build -c Release --verbosity normal
CIYML

echo ""
echo "✓ Scaffold complete. Verify with: find . -not -path './.git/*' | sort"
echo ""
echo "Next steps:"
echo "  git add ."
echo "  git commit -m \"chore: initial project scaffold\""
echo "  git push origin main"

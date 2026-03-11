using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.GameServer.Services;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.GameServer.Hubs
{
    public class GameHub : Hub
    {
        private readonly IJwtValidationService _jwtValidation;
        private readonly IMatchSessionService _sessionService;
        private readonly IMatchSimulationService _simulationService;
        private readonly ILogger<GameHub> _logger;

        public GameHub(
            IJwtValidationService jwtValidation,
            IMatchSessionService sessionService,
            IMatchSimulationService simulationService,
            ILogger<GameHub> logger)
        {
            _jwtValidation = jwtValidation;
            _sessionService = sessionService;
            _simulationService = simulationService;
            _logger = logger;
        }

        // ── Connection ────────────────────────────────────────────────────────

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            string? token = httpContext?.Request.Query["access_token"];

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Connection attempt without token. Aborting {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            var claims = _jwtValidation.Validate(token);
            if (claims == null)
            {
                _logger.LogWarning("Invalid JWT for {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            Context.Items["PlayerId"] = claims.PlayerId;
            Context.Items["SteamId"] = claims.SteamId;
            Context.Items["DisplayName"] = claims.DisplayName;

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Connection {ConnectionId} disconnected.", Context.ConnectionId);
            _sessionService.RemovePlayer(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // ── Lobby List ────────────────────────────────────────────────────────

        public Task<IReadOnlyList<LobbyInfoDto>> GetLobbies()
        {
            var open = _sessionService.GetOpenSessions();
            var dtos = open.Select(s => new LobbyInfoDto(
                s.SessionId,
                s.LobbyName,
                s.Players.FirstOrDefault(p => p.ConnectionId == s.HostConnectionId)?.DisplayName ?? "Unknown",
                s.Players.Count,
                s.MaxPlayers,
                s.LevelId
            )).ToList().AsReadOnly();

            return Task.FromResult<IReadOnlyList<LobbyInfoDto>>(dtos);
        }

        public async Task<LobbyCreatedDto> CreateLobby(string lobbyName)
        {
            var playerId = GetPlayerId();
            var steamId = GetSteamId();
            var displayName = GetDisplayName();

            if (string.IsNullOrWhiteSpace(lobbyName))
                lobbyName = $"{displayName}'s Lobby";

            var session = _sessionService.CreateSession(lobbyName);
            var player = new ConnectedPlayer(Context.ConnectionId, playerId, steamId, displayName);

            session.SetHost(Context.ConnectionId);
            _sessionService.AddPlayerToSession(session.SessionId, player);

            await Groups.AddToGroupAsync(Context.ConnectionId, session.SessionId.ToString());

            _logger.LogInformation("Player {PlayerId} created lobby {SessionId} \"{LobbyName}\"",
                playerId, session.SessionId, lobbyName);

            return new LobbyCreatedDto(session.SessionId);
        }

        public async Task<Guid?> JoinLobby(Guid sessionId)
        {
            var session = _sessionService.GetSession(sessionId);
            if (session == null)
            {
                await Clients.Caller.SendAsync("Error", "Lobby not found.");
                return null;
            }

            if (session.State != MatchState.Lobby)
            {
                await Clients.Caller.SendAsync("Error", "That match has already started.");
                return null;
            }

            if (session.Players.Count >= session.MaxPlayers)
            {
                await Clients.Caller.SendAsync("Error", "Lobby is full.");
                return null;
            }

            var playerId = GetPlayerId();
            var player = new ConnectedPlayer(
                Context.ConnectionId, playerId, GetSteamId(), GetDisplayName());

            _sessionService.AddPlayerToSession(sessionId, player);
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());

            await Clients.Group(sessionId.ToString())
                .SendAsync("LobbyStateUpdated", BuildLobbyState(session));

            return sessionId;
        }

        // ── In-Lobby ──────────────────────────────────────────────────────────

        public async Task ChangeMap(Guid sessionId, string levelId)
        {
            var session = _sessionService.GetSession(sessionId);
            if (session == null) return;

            bool changed = session.SetLevel(levelId, Context.ConnectionId);
            if (!changed)
            {
                await Clients.Caller.SendAsync("Error", "Map change rejected — not host, invalid level, or match already started.");
                return;
            }

            _logger.LogInformation("Session {SessionId} map changed to {LevelId} by host.", sessionId, levelId);
            await Clients.Group(sessionId.ToString())
                .SendAsync("LobbyStateUpdated", BuildLobbyState(session));
        }

        public async Task PlayerReady(Guid sessionId)
        {
            _logger.LogInformation("PlayerReady called: session={SessionId} connection={ConnectionId}",
                sessionId, Context.ConnectionId);

            var session = _sessionService.GetSession(sessionId);
            if (session == null)
            {
                _logger.LogWarning("PlayerReady: session {SessionId} not found.", sessionId);
                return;
            }

            bool shouldStart = false;
            string? startError = null;

            // IsReady must be set INSIDE the lock to avoid the race where both players
            // enter the ready check before either has set their flag.
            lock (session.Players)
            {
                var player = session.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                if (player == null)
                {
                    _logger.LogWarning("PlayerReady: connection {ConnectionId} not found in session {SessionId}",
                        Context.ConnectionId, sessionId);
                    return;
                }

                player.IsReady = true;

                bool allReady = session.Players.All(p => p.IsReady);
                _logger.LogInformation("PlayerReady: {Count} players, allReady={AllReady}, state={State}, levelId={LevelId}",
                    session.Players.Count, allReady, session.State, session.LevelId ?? "null");

                if (allReady && session.Players.Count >= 2 && session.State == MatchState.Lobby)
                {
                    if (session.LevelId == null)
                    {
                        startError = "Cannot start — no map selected. Host must choose a map first.";
                    }
                    else
                    {
                        session.Start();
                        shouldStart = true;
                    }
                }
            }

            if (startError != null)
            {
                await Clients.Caller.SendAsync("Error", startError);
                return;
            }

            await Clients.Group(sessionId.ToString())
                .SendAsync("LobbyStateUpdated", BuildLobbyState(session));

            if (shouldStart)
            {
                _logger.LogInformation("Session {SessionId} starting on level {LevelId}.",
                    sessionId, session.LevelId);
                await Clients.Group(sessionId.ToString()).SendAsync("MatchStarted", sessionId);
            }
        }

        // ── In-Match ──────────────────────────────────────────────────────────

        public Task UpdatePosition(PositionUpdateDto dto)
        {
            var session = _sessionService.GetSession(dto.SessionId);
            if (session?.State != MatchState.Active) return Task.CompletedTask;

            var player = session.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player != null)
            {
                player.X = dto.X;
                player.Y = dto.Y;
            }
            return Task.CompletedTask;
        }

        public async Task RequestAction(StationActionRequest request)
        {
            var session = _sessionService.GetSession(request.SessionId);
            if (session?.State != MatchState.Active) return;

            var player = session.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            switch (request.ActionType)
            {
                case StationActionType.Pickup:
                    await HandlePickup(session, player, request.StationId);
                    break;
                case StationActionType.Deposit:
                    await HandleDeposit(session, player, request.StationId);
                    break;
                case StationActionType.BeginPrep:
                    await HandleBeginPrep(session, player, request.StationId);
                    break;
                case StationActionType.CancelPrep:
                    await HandleCancelPrep(session, player, request.StationId);
                    break;
                case StationActionType.Collect:
                    await HandleCollect(session, player, request.StationId);
                    break;
                case StationActionType.Deliver:
                    await HandleDeliver(session, player, request.StationId);
                    break;
            }
        }

        // ── Station Handlers ──────────────────────────────────────────────────

        private async Task HandlePickup(MatchSession session, ConnectedPlayer player, string stationId)
        {
            if (!session.Stations.TryGetValue(stationId, out var station)) return;
            if (station.Type != StationType.IngredientSource) return;
            if (player.HeldItem != null) return;

            player.HeldItem = new HeldItem(station.SourceIngredient!.Value);
            await BroadcastMatchState(session);
        }

        private async Task HandleDeposit(MatchSession session, ConnectedPlayer player, string stationId)
        {
            if (!session.Stations.TryGetValue(stationId, out var station)) return;
            if (player.HeldItem == null) return;
            if (station.HeldItem != null) return;

            station.HeldItem = player.HeldItem;
            player.HeldItem = null;

            if (station.Type == StationType.Stove)
                station.BeginProcessing(10f);

            await BroadcastMatchState(session);
        }

        private async Task HandleBeginPrep(MatchSession session, ConnectedPlayer player, string stationId)
        {
            if (!session.Stations.TryGetValue(stationId, out var station)) return;
            if (station.Type != StationType.ChoppingBoard) return;
            if (station.HeldItem == null) return;
            if (station.OccupyingPlayerId != null) return;

            station.OccupyingPlayerId = player.PlayerId;
            station.BeginProcessing(5f);
            await BroadcastMatchState(session);
        }

        private async Task HandleCancelPrep(MatchSession session, ConnectedPlayer player, string stationId)
        {
            if (!session.Stations.TryGetValue(stationId, out var station)) return;
            if (station.OccupyingPlayerId != player.PlayerId) return;

            station.ResetProgress();
            station.OccupyingPlayerId = null;
            await BroadcastMatchState(session);
        }

        private async Task HandleCollect(MatchSession session, ConnectedPlayer player, string stationId)
        {
            if (!session.Stations.TryGetValue(stationId, out var station)) return;
            if (!station.IsComplete) return;
            if (player.HeldItem != null) return;

            player.HeldItem = station.HeldItem;
            station.HeldItem = null;
            station.ResetProgress();
            station.OccupyingPlayerId = null;
            await BroadcastMatchState(session);
        }

        private async Task HandleDeliver(MatchSession session, ConnectedPlayer player, string stationId)
        {
            if (!session.Stations.TryGetValue(stationId, out var station)) return;
            if (station.Type != StationType.DeliveryCounter) return;
            if (player.HeldItem == null) return;

            var result = _simulationService.TryDeliver(session, new List<Ingredient> { player.HeldItem.Ingredient });

            if (result.Success)
            {
                player.HeldItem = null;
                player.Score += result.Score;
                session.TotalScore += result.Score;
                if (result.IsPerfect) session.PerfectOrders++;
                session.CompletedOrders++;
            }

            await Clients.Caller.SendAsync("DeliveryResult", result);
            await BroadcastMatchState(session);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private LobbyStateDto BuildLobbyState(MatchSession session)
        {
            var players = session.Players.Select(p => new LobbyPlayerDto(
                p.PlayerId,
                p.DisplayName,
                p.IsReady,
                p.ConnectionId == session.HostConnectionId
            )).ToList().AsReadOnly();

            return new LobbyStateDto(session.SessionId, session.LevelId, players);
        }

        private async Task BroadcastMatchState(MatchSession session)
        {
            var state = new MatchStateDto(
                session.SessionId,
                session.Players.Select(p => new PlayerPositionDto(p.PlayerId, p.DisplayName, p.X, p.Y)).ToList().AsReadOnly(),
                session.Stations.Values.Select(s => new StationStateDto(
                    s.StationId,
                    s.Type.ToString(),
                    s.HeldItem?.Ingredient.ToString(),
                    s.HeldItem?.PrepState.ToString(),
                    s.ProgressNormalized,
                    s.OccupyingPlayerId != null
                )).ToList().AsReadOnly(),
                session.TimeRemainingSeconds,
                session.TotalScore
            );

            await Clients.Group(session.SessionId.ToString()).SendAsync("MatchStateUpdated", state);
        }

        private Guid GetPlayerId() => (Guid)Context.Items["PlayerId"]!;
        private string GetSteamId() => (string)Context.Items["SteamId"]!;
        private string GetDisplayName() => (string)Context.Items["DisplayName"]!;
    }
}
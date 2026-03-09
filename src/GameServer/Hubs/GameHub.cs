using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.GameServer.Services;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.GameServer.Hubs
{
    public class GameHub : Hub
    {
        private readonly IJwtValidationService _jwtValidation;
        private readonly IMatchSessionService _sessionService;
        private readonly IMatchSimulationService _matchSimulation;
        private readonly ILogger<GameHub> _logger;

        public GameHub(
            IJwtValidationService jwtValidation,
            IMatchSessionService sessionService,
            IMatchSimulationService matchSimulation,
            ILogger<GameHub> logger)
        {
            _jwtValidation = jwtValidation;
            _sessionService = sessionService; 
            _matchSimulation = matchSimulation;
            _logger = logger;
        }


        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            string? token = httpContext?.Request.Query["access_token"];

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Connection attempt without a token. Aborting {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            var claims = _jwtValidation.Validate(token);


            if (claims == null)
            {
                _logger.LogWarning("Invalid JWT provided for {ConnectionId}", Context.ConnectionId);
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

        public async Task JoinMatch(string levelId)
        {
            var playerId = (Guid)Context.Items["PlayerId"]!;
            var steamId = (string)Context.Items["SteamId"]!;
            var displayName = (string)Context.Items["DisplayName"]!;

            var player = new ConnectedPlayer(Context.ConnectionId, playerId, steamId, displayName);


            var session = _sessionService.GetOrCreateSession(levelId);

            // Assign the host if one hasn't been set yet
            session.SetHost(Context.ConnectionId);

            _sessionService.AddPlayerToSession(session.SessionId, player);

            await Groups.AddToGroupAsync(Context.ConnectionId, session.SessionId.ToString());
            await Clients.Caller.SendAsync("JoinedMatch", session.SessionId);

            // Broadcast the current lobby state to everyone in the session
            await Clients.Group(session.SessionId.ToString()).SendAsync("LobbyStateUpdated", BuildLobbyState(session));
        }

        public async Task ChangeMap(Guid sessionId, string levelId)
        {
            var session = _sessionService.GetSession(sessionId);
            if (session == null) return;

            // session.SetLevel handles the host validation logic
            session.SetLevel(levelId, Context.ConnectionId);

            // Broadcast updated state to reflect the new map
            await Clients.Group(sessionId.ToString()).SendAsync("LobbyStateUpdated", BuildLobbyState(session));
        }

        public async Task DeliverDish(Guid sessionId, List<Ingredient> ingredients)
        {
            var playerId = (Guid)Context.Items["PlayerId"]!;

            var result = _matchSimulation.DeliverDish(sessionId, playerId, ingredients);

            // Send delivery outcome only to the player who submitted
            await Clients.Caller.SendAsync("DeliveryResult", result);

            // Broadcast updated match state to everyone in the session
            var session = _sessionService.GetSession(sessionId);
            if (session != null)
            {
                await Clients.Group(sessionId.ToString())
                    .SendAsync("MatchStateUpdated", BuildMatchState(session));
            }
        }

        public async Task PlayerReady(Guid sessionId)
        {
            var session = _sessionService.GetSession(sessionId);
            if (session == null) return;

            var player = session.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            player.IsReady = true;

            bool shouldStart = false;


            lock (session.Players)
            {
                bool allReady = session.Players.All(p => p.IsReady);
                if (allReady && session.Players.Count >= 2 && session.State == MatchState.Lobby)
                {
                    session.Start();
                    shouldStart = true;
                }
            }

            // Always broadcast state so players see readiness updates
            await Clients.Group(sessionId.ToString()).SendAsync("LobbyStateUpdated", BuildLobbyState(session));

            // Always broadcast state so players see readiness updates
            await Clients.Group(sessionId.ToString()).SendAsync("LobbyStateUpdated", BuildLobbyState(session));

            if (shouldStart)
            {
                _logger.LogInformation("Session {SessionId} conditions met. Starting match.", sessionId);
                await Clients.Group(sessionId.ToString()).SendAsync("MatchStarted", sessionId);
            }
        }

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

        private MatchStateDto BuildMatchState(MatchSession session)
        {
            List<ActiveOrderDto> orders;
            lock (session.Orders)
            {
                orders = session.Orders
                    .Where(o => o.Status == OrderStatus.InProgress)
                    .Select(o => new ActiveOrderDto(
                        o.OrderId,
                        o.Recipe.Name,
                        o.Recipe.RequiredIngredients.Select(i => i.ToString()).ToList().AsReadOnly(),
                        o.Timer.TimeRemaining,
                        o.Timer.TotalDuration
                    ))
                    .ToList();
            }

            // MatchPlayerDto carries live score and orders delivered — not lobby fields
            var players = session.Players.Select(p => new MatchPlayerDto(
                p.PlayerId,
                p.DisplayName,
                p.Score,
                p.OrdersDelivered
            )).ToList().AsReadOnly();

            return new MatchStateDto(
                session.SessionId,
                session.State.ToString(),
                session.TimeRemainingSeconds,
                session.TotalScore,
                orders,
                players
            );
        }
    }
}
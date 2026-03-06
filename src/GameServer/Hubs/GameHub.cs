using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.GameServer.Services;
using KitchenOrchestrator.Shared.Contracts.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.GameServer.Hubs
{
    public class GameHub : Hub
    {
        private readonly IJwtValidationService _jwtValidation;
        private readonly IMatchSessionService _sessionService;
        private readonly ILogger<GameHub> _logger;

        public GameHub(
            IJwtValidationService jwtValidation,
            IMatchSessionService sessionService,
            ILogger<GameHub> logger)
        {
            _jwtValidation = jwtValidation;
            _sessionService = sessionService;
            _logger = logger;
        }
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            // Use the null-coalescing operator or a direct check
            string? token = httpContext?.Request.Query["access_token"];

            // heck if token is null or empty first
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Connection attempt without a token. Aborting {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            // Now 'token' is guaranteed not to be null for the Validate method
            var claims = _jwtValidation.Validate(token);
            
            if (claims == null)
            {
                _logger.LogWarning("Invalid JWT provided for {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }

            // Store claims...
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
            _sessionService.AddPlayerToSession(session.SessionId, player);

            await Groups.AddToGroupAsync(Context.ConnectionId, session.SessionId.ToString());
            await Clients.Caller.SendAsync("JoinedMatch", session.SessionId);
        }

        public async Task PlayerReady(Guid sessionId)
        {
            var session = _sessionService.GetSession(sessionId);
            if (session == null) return;

            var player = session.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            player.IsReady = true;

            bool shouldStart = false;
            
            // Fixed: Lock logic only wraps synchronous state changes to prevent async deadlocks
            lock (session.Players)
            {
                bool allReady = session.Players.All(p => p.IsReady);
                if (allReady && session.Players.Count >= 2 && session.State == MatchState.Lobby)
                {
                    session.Start();
                    shouldStart = true;
                }
            }

            if (shouldStart)
            {
                _logger.LogInformation("Session {SessionId} conditions met. Starting match.", sessionId);
                await Clients.Group(sessionId.ToString()).SendAsync("MatchStarted", sessionId);
            }
        }
    }
}
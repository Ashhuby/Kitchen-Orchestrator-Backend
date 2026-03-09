using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Levels;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KitchenOrchestrator.GameServer.Services
{
    public interface IMatchSessionService
    {
        MatchSession GetOrCreateSession(string levelId);
        MatchSession? GetSession(Guid sessionId);
        IReadOnlyList<MatchSession> GetActiveSessions();
        void AddPlayerToSession(Guid sessionId, ConnectedPlayer player);
        void RemovePlayer(string connectionId);
    }

    public class MatchSessionService : IMatchSessionService
    {
        private readonly ConcurrentDictionary<Guid, MatchSession> _sessions = new();
        private readonly ILogger<MatchSessionService> _logger;

        public MatchSessionService(ILogger<MatchSessionService> logger)
        {
            _logger = logger;
        }

        public MatchSession GetOrCreateSession(string levelId)
        {
            _logger.LogInformation(
                "GetOrCreateSession called for {LevelId}. Total sessions: {Count}, Lobby sessions: {LobbyCount}",
                levelId,
                _sessions.Count,
                _sessions.Values.Count(s => s.LevelId == levelId && s.State == MatchState.Lobby));

            foreach (var s in _sessions.Values)
            {
                _logger.LogInformation(
                    "Existing session {Id} LevelId={Level} State={State} Players={Count}",
                    s.SessionId, s.LevelId, s.State, s.Players.Count);
            }

            var existingLobby = _sessions.Values.FirstOrDefault(s =>
                s.LevelId.Equals(levelId, StringComparison.OrdinalIgnoreCase) && 
                s.State == MatchState.Lobby);

            if (existingLobby != null)
                return existingLobby;

            var levelDef = LevelRegistry.GetById(levelId);
            if (levelDef == null)
                throw new ArgumentException($"Level with ID {levelId} does not exist.");

            var newSession = new MatchSession(levelDef);
            _sessions.TryAdd(newSession.SessionId, newSession);

            _logger.LogInformation("Created new MatchSession {SessionId} for Level {LevelId}",
                newSession.SessionId, levelId);

            return newSession;
        }

        public MatchSession? GetSession(Guid sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }

        public IReadOnlyList<MatchSession> GetActiveSessions()
        {
            return _sessions.Values
                .Where(s => s.State == MatchState.Active)
                .ToList()
                .AsReadOnly();
        }

        public void AddPlayerToSession(Guid sessionId, ConnectedPlayer player)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                lock (session.Players)
                {
                    if (!session.Players.Any(p => p.PlayerId == player.PlayerId))
                    {
                        session.Players.Add(player);
                    }
                }
            }
        }

        public void RemovePlayer(string connectionId)
        {
            foreach (var session in _sessions.Values)
            {
                lock (session.Players)
                {
                    var player = session.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
                    if (player != null)
                    {
                        session.Players.Remove(player);
                        _logger.LogInformation("Player {PlayerId} removed from Session {SessionId}",
                            player.PlayerId, session.SessionId);

                        // NOTE: Lobby cleanup removed - was deleting sessions too aggressively
                        // during reconnect flows. Will re-add with proper grace period later.
                        break;
                    }
                }
            }
        }
    }
}
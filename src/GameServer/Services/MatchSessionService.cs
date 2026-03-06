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
            // Find existing Lobby
            var existingLobby = _sessions.Values.FirstOrDefault(s => 
                s.LevelId == levelId && s.State == MatchState.Lobby);

            if (existingLobby != null) return existingLobby;

            // Create new if registry allows
            var levelDef = LevelRegistry.GetById(levelId);
            if (levelDef == null)
            {
                throw new ArgumentException($"Level with ID {levelId} does not exist.");
            }

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
            // ToList() creates a snapshot for the caller
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
            // Safe to iterate and TryRemove thanks to ConcurrentDictionarys implementation yayyy
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
                        
                        // Self-cleaning: If the lobby is empty, burn the room down.
                        if (session.Players.Count == 0 && session.State == MatchState.Lobby)
                        {
                            _sessions.TryRemove(session.SessionId, out _);
                            _logger.LogDebug("Cleanup: Removed empty lobby {SessionId}", session.SessionId);
                        }
                    }
                }
            }
        }
    }
}
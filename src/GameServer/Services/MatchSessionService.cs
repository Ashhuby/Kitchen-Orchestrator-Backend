using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.Shared.Contracts.Enums;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KitchenOrchestrator.GameServer.Services
{
    public interface IMatchSessionService
    {
        MatchSession CreateSession(string lobbyName);
        MatchSession? GetSession(Guid sessionId);
        IReadOnlyList<MatchSession> GetOpenSessions();
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

        /// <summary>
        /// Explicitly creates a new named lobby. Sessions are never auto-created;
        /// the host must call this from the lobby list screen.
        /// </summary>
        public MatchSession CreateSession(string lobbyName)
        {
            var session = new MatchSession(lobbyName);
            _sessions.TryAdd(session.SessionId, session);
            _logger.LogInformation("Created session {SessionId} \"{LobbyName}\"",
                session.SessionId, lobbyName);
            return session;
        }

        public MatchSession? GetSession(Guid sessionId)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? session : null;
        }

        /// <summary>
        /// Returns all sessions in Lobby state that still have room.
        /// This is what the lobby list screen shows.
        /// </summary>
        public IReadOnlyList<MatchSession> GetOpenSessions()
        {
            return _sessions.Values
                .Where(s => s.State == MatchState.Lobby && s.Players.Count < s.MaxPlayers)
                .OrderByDescending(s => s.Players.Count) // Fuller lobbies first
                .ToList()
                .AsReadOnly();
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
            if (!_sessions.TryGetValue(sessionId, out var session)) return;

            lock (session.Players)
            {
                if (session.Players.Count >= session.MaxPlayers)
                {
                    _logger.LogWarning("Player {PlayerId} tried to join full session {SessionId}",
                        player.PlayerId, sessionId);
                    return;
                }

                if (!session.Players.Any(p => p.PlayerId == player.PlayerId))
                {
                    session.Players.Add(player);
                    _logger.LogInformation("Player {PlayerId} joined session {SessionId} ({Count}/{Max})",
                        player.PlayerId, sessionId, session.Players.Count, session.MaxPlayers);
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
                    if (player == null) continue;

                    session.Players.Remove(player);
                    _logger.LogInformation("Player {PlayerId} removed from session {SessionId}",
                        player.PlayerId, session.SessionId);

                    // If the host left, assign the next player as host
                    if (connectionId == session.HostConnectionId && session.Players.Count > 0)
                    {
                        // Access HostConnectionId via SetHost — but SetHost only sets if empty.
                        // We need to force-reassign here. This is a valid reason to expose
                        // a ReassignHost method rather than hacking around SetHost's guard.
                        session.ReassignHost(session.Players[0].ConnectionId);
                        _logger.LogInformation("Host left session {SessionId}. New host: {ConnectionId}",
                            session.SessionId, session.Players[0].ConnectionId);
                    }

                    // Clean up empty sessions so the lobby list stays tidy
                    if (session.Players.Count == 0 && session.State == MatchState.Lobby)
                    {
                        _sessions.TryRemove(session.SessionId, out _);
                        _logger.LogInformation("Empty session {SessionId} removed.", session.SessionId);
                    }

                    break;
                }
            }
        }
    }
}
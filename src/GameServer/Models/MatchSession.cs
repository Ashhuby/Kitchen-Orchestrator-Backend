using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Levels;

namespace KitchenOrchestrator.GameServer.Models
{
    public class MatchSession
    {
        public Guid SessionId { get; }
        public string LobbyName { get; }
        public int MaxPlayers { get; } = 4;

        // Null until the host selects a map in the lobby.
        // Start() will throw if this is still null when called.
        public string? LevelId { get; private set; }
        public LevelDefinition? LevelDefinition { get; private set; }

        public string HostConnectionId { get; private set; } = string.Empty;
        public MatchState State { get; set; }
        public DateTime StartedAtUtc { get; private set; }
        public float TimeRemainingSeconds { get; set; }
        public int TotalScore { get; set; }
        public List<ConnectedPlayer> Players { get; } = new();
        public List<ActiveOrder> Orders { get; } = new();
        public int CompletedOrders { get; set; }
        public int FailedOrders { get; set; }
        public int PerfectOrders { get; set; }
        public float TimeSinceLastOrderSpawn { get; set; }
        public Dictionary<string, StationState> Stations { get; } = new();

        public MatchSession(string lobbyName)
        {
            SessionId = Guid.NewGuid();
            LobbyName = lobbyName;
            State = MatchState.Lobby;
        }

        public void SetHost(string connectionId)
        {
            if (string.IsNullOrEmpty(HostConnectionId))
                HostConnectionId = connectionId;
        }

        /// <summary>
        /// Force-reassigns the host. Used when the current host disconnects.
        /// Only callable from MatchSessionService — not exposed to the hub.
        /// </summary>
        internal void ReassignHost(string connectionId)
        {
            HostConnectionId = connectionId;
        }

        /// <summary>
        /// Sets the level. Only the host can call this and only while in Lobby state.
        /// Returns false if the requestor is not the host, the level doesn't exist,
        /// or the session is no longer in Lobby state.
        /// </summary>
        public bool SetLevel(string levelId, string requestingConnectionId)
        {
            if (requestingConnectionId != HostConnectionId) return false;
            if (State != MatchState.Lobby) return false;

            var levelDef = LevelRegistry.GetById(levelId);
            if (levelDef == null) return false;

            LevelId = levelDef.LevelId;
            LevelDefinition = levelDef;
            TimeRemainingSeconds = levelDef.DurationSeconds;
            return true;
        }

        /// <summary>
        /// Transitions to Active. Throws if no level has been set — the hub must
        /// validate this before calling Start().
        /// </summary>
        public void Start()
        {
            if (LevelDefinition == null)
                throw new InvalidOperationException(
                    $"Cannot start session {SessionId} — no level has been selected.");

            State = MatchState.Active;
            StartedAtUtc = DateTime.UtcNow;
        }
    }
}
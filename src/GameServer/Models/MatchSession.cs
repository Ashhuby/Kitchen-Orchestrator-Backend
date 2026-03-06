using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Levels;

namespace KitchenOrchestrator.GameServer.Models
{
    public class MatchSession
    {
        public Guid SessionId { get; }
        public string LevelId { get; }
        public MatchState State { get; set; }
        public DateTime StartedAtUtc { get; private set; }
        public float TimeRemainingSeconds { get; set; }
        public int TotalScore { get; set; }
        public List<ConnectedPlayer> Players { get; }
        public List<ActiveOrder> Orders { get; }
        public int CompletedOrders { get; set; } = 0;
        public int FailedOrders { get; set; } = 0;
        public int PerfectOrders { get; set; } = 0;

        public MatchSession(LevelDefinition level)
        {
            SessionId = Guid.NewGuid();
            LevelId = level.LevelId; // Pulled directly from the definition
            State = MatchState.Lobby;
            TimeRemainingSeconds = level.DurationSeconds;
            Players = new List<ConnectedPlayer>();
            Orders = new List<ActiveOrder>();
        }

        public void Start()
        {
            State = MatchState.Active;
            StartedAtUtc = DateTime.UtcNow;
        }
    }
}
using KitchenOrchestrator.GameServer.Models;

namespace KitchenOrchestrator.GameServer.Models
{
    public class ConnectedPlayer
    {
        public string ConnectionId { get; set; } = string.Empty;
        public Guid PlayerId { get; }
        public string SteamId { get; } = string.Empty;
        public string DisplayName { get; } = string.Empty;

        // Match scoring
        public int Score { get; set; }
        public int OrdersDelivered { get; set; }

        // Lobby
        public bool IsReady { get; set; }

        // In-match position (updated by UpdatePosition hub messages)
        public float X { get; set; }
        public float Y { get; set; }

        // The item the player is currently carrying. Null if hands are empty.
        public HeldItem? HeldItem { get; set; }

        public ConnectedPlayer(string connectionId, Guid playerId, string steamId, string displayName)
        {
            ConnectionId = connectionId;
            PlayerId = playerId;
            SteamId = steamId;
            DisplayName = displayName;
        }
    }
}
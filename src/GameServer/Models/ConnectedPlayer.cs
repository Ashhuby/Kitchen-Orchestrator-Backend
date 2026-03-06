namespace KitchenOrchestrator.GameServer.Models
{
    public class ConnectedPlayer
    {
        public string ConnectionId { get; set; } = string.Empty;
        public Guid PlayerId { get; }
        public string SteamId { get; } = string.Empty;
        public string DisplayName { get; } = string.Empty;
        public int Score { get; set; }
        public int OrdersDelivered { get; set; }
        public bool IsReady { get; set; }

        public ConnectedPlayer(string connectionId, Guid playerId, string steamId, string displayName)
        {
            ConnectionId = connectionId;
            PlayerId = playerId;
            SteamId = steamId;
            DisplayName = displayName;
        }

    }
}
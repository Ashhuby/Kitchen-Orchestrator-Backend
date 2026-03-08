namespace KitchenOrchestrator.GameClient.Configuration
{
    public class GameClientOptions
    {
        public string IdentityApiBaseUrl { get; set; } = string.Empty;
        public string GameServerHubUrl { get; set; } = string.Empty;
        public int SteamAppId { get; set; } = 480; // Space wars go brr
    }
}
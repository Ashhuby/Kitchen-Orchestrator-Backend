namespace KitchenOrchestrator.IdentityAPI.Configuration
{
    public class SteamOptions
    {
        public string PublisherKey { get; set; }  = string.Empty;   // Steam Web API key from partner.steamgames.com
        public string AppId  { get; set; }  = string.Empty;
        public string TicketVerificationUrl { get; set; }  = string.Empty;      
    }
}
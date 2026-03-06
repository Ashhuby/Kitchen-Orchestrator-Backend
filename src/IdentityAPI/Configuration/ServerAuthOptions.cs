namespace KitchenOrchestrator.IdentityAPI.Configuration
{
    public class ServerAuthOptions
    {
        public string SharedSecret { get; set; } = string.Empty; // Used by HmacHelper
    }
}
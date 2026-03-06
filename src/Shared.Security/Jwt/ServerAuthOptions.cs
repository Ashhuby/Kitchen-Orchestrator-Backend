namespace KitchenOrchestrator.Shared.Security.Jwt
{
    public class ServerAuthOptions
    {
        public string SharedSecret { get; set; } = string.Empty; // Used by HmacHelper
    }
}
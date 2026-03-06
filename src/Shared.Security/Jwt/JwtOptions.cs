namespace KitchenOrchestrator.Shared.Security.Jwt
{
    public class JwtOptions
    {
       public string SigningKey { get; set; }  = string.Empty;
       public string Issuer { get; set; }  = string.Empty;
       public string Audience { get; set; }  = string.Empty;
       public int ExpiryHours { get; set; } = 1;   
    }
}
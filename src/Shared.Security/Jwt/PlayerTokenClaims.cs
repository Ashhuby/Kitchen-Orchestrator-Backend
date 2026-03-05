namespace KitchenOrchestrator.Shared.Security.Jwt
{
    public record PlayerTokenClaims(Guid PlayerId, string SteamId, string DisplayName); // This is what is returned when JWT is validated ( i dont want to return just strings that can bug out )
}   
using KitchenOrchestrator.Shared.Security.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace KitchenOrchestrator.GameServer.Services
{
    public interface IJwtValidationService
    {
        PlayerTokenClaims? Validate(string? token);
    }

    public class JwtValidationService : IJwtValidationService
    {
        private readonly JwtOptions _jwtOptions;
        private readonly IWebHostEnvironment _environment;

        public JwtValidationService(IOptions<JwtOptions> jwtOptions, IWebHostEnvironment environment)
        {
            _jwtOptions = jwtOptions.Value;
            _environment = environment;
        }

        public PlayerTokenClaims? Validate(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            // Dev bypass - Parse the client-generated GUID from the token string bleh
            if (_environment.IsDevelopment() && token.StartsWith("dev-mock-jwt-token:"))
            {
                var parts = token.Split(':');
                if (parts.Length > 1 && Guid.TryParse(parts[1], out var playerId))
                {
                    return new PlayerTokenClaims(
                        playerId, 
                        "dev-steam-id",
                        "DevPlayer"
                    );
                }
            }

            // Standard cryptographic check via Shared.Security
            return JwtUtility.ValidateToken(
                token, 
                _jwtOptions.SigningKey, 
                _jwtOptions.Issuer, 
                _jwtOptions.Audience
            );
        }
    }
}
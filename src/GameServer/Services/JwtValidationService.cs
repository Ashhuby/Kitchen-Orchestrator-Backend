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
        
        // A stable GUID for the DevPlayer to ensure identity consistency during a session
        private static readonly Guid DevPlayerId = Guid.Parse("00000000-0000-0000-0000-000000000001");

        public JwtValidationService(IOptions<JwtOptions> jwtOptions, IWebHostEnvironment environment)
        {
            _jwtOptions = jwtOptions.Value;
            _environment = environment;
        }

        public PlayerTokenClaims? Validate(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            // Dev bypass - only active if ASPNETCORE_ENVIRONMENT is 'Development'
            if (_environment.IsDevelopment() && token == "dev-mock-jwt-token")
            {
                return new PlayerTokenClaims(
                    DevPlayerId,
                    "dev-steam-id",
                    "DevPlayer"
                );
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
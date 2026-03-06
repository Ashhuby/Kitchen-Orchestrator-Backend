using KitchenOrchestrator.Shared.Security.Jwt; // New home for JwtOptions
using Microsoft.Extensions.Options;

namespace KitchenOrchestrator.GameServer.Services
{
    public interface IJwtValidationService
    {
        PlayerTokenClaims? Validate(string token);
    }

    public class JwtValidationService : IJwtValidationService
    {
        private readonly JwtOptions _jwtOptions;

        public JwtValidationService(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions.Value;
        }

        public PlayerTokenClaims? Validate(string token)
        {
            return JwtUtility.ValidateToken(
                token, 
                _jwtOptions.SigningKey, 
                _jwtOptions.Issuer, 
                _jwtOptions.Audience
            );
        }
    }
}
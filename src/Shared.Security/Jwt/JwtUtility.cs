using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
namespace KitchenOrchestrator.Shared.Security.Jwt
{
    public static class JwtUtility
    {
         public static string IssueToken(Guid playerId, string steamId, string displayName, string signingKey, string issuer, string audience, TimeSpan expiry)
        {
            if(signingKey.Length < 32) throw new ArgumentException("The signing key must be at least 32 characters long for HMAC-SHA256.");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.PlayerId, playerId.ToString()),
                new Claim(ClaimTypes.SteamId, steamId),
                new Claim(ClaimTypes.DisplayName, displayName)
            };

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.Add(expiry),
                signingCredentials: credentials
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static PlayerTokenClaims? ValidateToken(string token, string signingKey, string issuer, string audience)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(signingKey);

                // Define TokenValidationParameters
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true, // Ensures the token hasn't expired
                    ClockSkew = TimeSpan.FromSeconds(30) // Change the default 5-minute grace period to 30 seconds huzzah thanks microslop!
                };

                // Call handler.ValidateToken()
                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                // 4. Extract claims from the principal
                var playerIdStr = principal.FindFirst(ClaimTypes.PlayerId)?.Value;
                var steamId = principal.FindFirst(ClaimTypes.SteamId)?.Value;
                var displayName = principal.FindFirst(ClaimTypes.DisplayName)?.Value;

                // Return null if any claim is missing
                if (string.IsNullOrEmpty(playerIdStr) || string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(displayName))
                {
                    return null;
                }

                if (!Guid.TryParse(playerIdStr, out Guid playerId))
                {
                    return null;
                }

                // Then we return new PlayerTokenClaims record if it survived all that ^
                return new PlayerTokenClaims(playerId, steamId, displayName);
            }
            catch
            {
                // If the signature is invalid - token is expired or malformed or i just dont like you return null
                return null;
            }
        }
    } 
}
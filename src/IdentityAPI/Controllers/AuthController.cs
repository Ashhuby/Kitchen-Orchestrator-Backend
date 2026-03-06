using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Security.Jwt;
using KitchenOrchestrator.IdentityAPI.Configuration;
using KitchenOrchestrator.IdentityAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KitchenOrchestrator.IdentityAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ISteamAuthService _steamAuth;
        private readonly IPlayerService _playerService;
        private readonly JwtOptions _jwtOptions;
        private readonly SteamOptions _steamOptions;

        public AuthController(
            ISteamAuthService steamAuth,
            IPlayerService playerService,
            IOptions<JwtOptions> jwtOptions,
            IOptions<SteamOptions> steamOptions)
        {
            _steamAuth = steamAuth;
            _playerService = playerService;
            _jwtOptions = jwtOptions.Value;
            _steamOptions = steamOptions.Value;
        }

        [HttpPost("steam")]
        public async Task<IActionResult> AuthenticateSteam([FromBody] SteamAuthRequest request)
        {
            // Guard: AppId check
            if (request.AppId != _steamOptions.AppId)
            {
                return Unauthorized("Invalid AppId provided.");
            }

            // Verify: Using the correct property name 'HexEncodedTicket' from DTO
            var steamId = await _steamAuth.VerifyTicketAsync(request.HexEncodedTicket, request.AppId);
            
            if (string.IsNullOrEmpty(steamId))
            {
                return Unauthorized("Steam ticket verification failed.");
            }

            // Resolve: DisplayName now included in the request body
            var player = await _playerService.GetOrCreatePlayerAsync(steamId, request.DisplayName);

            // Issue JWT
            var token = JwtUtility.IssueToken(
                player.Id,
                player.SteamId,
                player.DisplayName,
                _jwtOptions.SigningKey,
                _jwtOptions.Issuer,
                _jwtOptions.Audience,
                TimeSpan.FromHours(_jwtOptions.ExpiryHours)
            );

            // Map to DTO
            var profileDto = new PlayerProfileDto(
                player.Id,
                player.DisplayName,
                player.MatchesPlayed,
                player.MatchesWon,
                player.TotalScore,
                player.PerfectOrders
            );

            // Return: Now including the required TokenExpirationUtc for the AuthResponse
            var expiration = DateTime.UtcNow.AddHours(_jwtOptions.ExpiryHours);
            
            return Ok(new AuthResponse(token, expiration, profileDto));
        }
    }
}
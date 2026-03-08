using KitchenOrchestrator.GameClient.Configuration;
using KitchenOrchestrator.GameClient.Models;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using System.Net.Http.Json;

namespace KitchenOrchestrator.GameClient.Auth
{
    public class AuthManager
    {
        private readonly HttpClient _httpClient;
        private readonly GameClientOptions _options;
        private readonly ClientState _state;

        public AuthManager(HttpClient httpClient, GameClientOptions options, ClientState state)
        {
            _httpClient = httpClient;
            _options = options;
            _state = state;
        }

        public async Task<bool> LoginAsync(string hexTicket, string displayName)
        {
            try
            {
                // SteamAuthRequest is a positional record: (string Ticket, string AppId, string DisplayName)
                var request = new SteamAuthRequest(hexTicket, _options.SteamAppId.ToString(), displayName);

                string url = $"{_options.IdentityApiBaseUrl}/api/auth/steam";
                
                var response = await _httpClient.PostAsJsonAsync(url, request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Login failed: {response.StatusCode}");
                    return false;
                }

                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

                if (authResponse == null) return false;

                // Mapping to state using the correct AuthResponse record properties
                _state.Jwt = authResponse.Jwt;
                _state.TokenExpiresUtc = authResponse.TokenExpirationUtc;
                _state.Profile = authResponse.PlayerProfileDto;
                _state.IsAuthenticated = true;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Authentication exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DevLoginAsync(string displayName)
        {
            // Mock auth bypass for development heheheh fake data
            _state.Jwt = "dev-mock-jwt-token";
            _state.TokenExpiresUtc = DateTime.UtcNow.AddHours(24);
            
            _state.Profile = new PlayerProfileDto(
                Guid.NewGuid(),    // Id
                displayName,       // DisplayName
                0,                 // MatchesPlayed
                0,                 // MatchesWon
                0,                 // TotalScore
                0                  // PerfectOrders
            );

            _state.IsAuthenticated = true;
            return await Task.FromResult(true);
        }

        public void Logout()
        {
            _state.Jwt = null;
            _state.TokenExpiresUtc = null;
            _state.Profile = null;
            _state.IsAuthenticated = false;
        }
    }
}
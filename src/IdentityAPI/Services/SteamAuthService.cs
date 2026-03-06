using KitchenOrchestrator.IdentityAPI.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.IdentityAPI.Services
{
    public interface ISteamAuthService
    {
        Task<string?> VerifyTicketAsync(string hexTicket, string appId);
    }

    public class SteamAuthService : ISteamAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly SteamOptions _steamOptions;
        private readonly ILogger<SteamAuthService> _logger;

        public SteamAuthService(
            HttpClient httpClient, 
            IOptions<SteamOptions> steamOptions,
            ILogger<SteamAuthService> logger)
        {
            _httpClient = httpClient;
            _steamOptions = steamOptions.Value;
            _logger = logger;
        }

        public async Task<string?> VerifyTicketAsync(string hexTicket, string appId)
        {
            try
            {
                // Build the Secure Request URL
                var url = $"{_steamOptions.TicketVerificationUrl}?key={_steamOptions.PublisherKey}&appid={appId}&ticket={hexTicket}";

                // Call Valve's Partner API
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Steam API returned non-success code: {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();

                // Deserialize the nested response
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var steamResponse = JsonSerializer.Deserialize<SteamResponse>(content, options);

                // Validate the "Result" field inside the JSON
                if (steamResponse?.Response?.Params?.Result == "OK")
                {
                    return steamResponse.Response.Params.SteamId;
                }

                _logger.LogWarning("Steam ticket verification failed. Result: {Result}", 
                    steamResponse?.Response?.Params?.Result ?? "No Response");
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Steam ticket verification");
                return null;
            }
        }

        // Internal records for JSON Mapping
        private record SteamResponse(SteamResponseBody Response);
        private record SteamResponseBody(SteamResponseParams? Params);
        private record SteamResponseParams(string Result, string SteamId);
    }
}
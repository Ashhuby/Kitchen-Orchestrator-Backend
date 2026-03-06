using KitchenOrchestrator.GameServer.Configuration;
using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.GameLogic.Levels;
using KitchenOrchestrator.Shared.Security.Hashing;
using KitchenOrchestrator.Shared.Security.Jwt; 
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace KitchenOrchestrator.GameServer.Services
{
    public interface IMatchResultSubmissionService
    {
        Task SubmitAsync(MatchSession session);
    }

    public class MatchResultSubmissionService : IMatchResultSubmissionService
    {
        private readonly HttpClient _httpClient;
        private readonly ServerAuthOptions _serverAuthOptions;
        private readonly GameServerOptions _gameServerOptions;
        private readonly ILogger<MatchResultSubmissionService> _logger;

        public MatchResultSubmissionService(
            HttpClient httpClient,
            IOptions<ServerAuthOptions> serverAuthOptions,
            IOptions<GameServerOptions> gameServerOptions,
            ILogger<MatchResultSubmissionService> logger)
        {
            _httpClient = httpClient;
            _serverAuthOptions = serverAuthOptions.Value;
            _gameServerOptions = gameServerOptions.Value;
            _logger = logger;
        }

        public async Task SubmitAsync(MatchSession session)
        {
            try
            {
                var levelDef = LevelRegistry.GetById(session.LevelId);
                var targetScore = levelDef?.TargetScore ?? 0;

                // Mapping to the Positional Record: MatchResultSubmission
                var submission = new MatchResultSubmission(
                    session.SessionId,
                    session.StartedAtUtc,
                    DateTime.UtcNow, // End time
                    session.LevelId,
                    session.TotalScore,
                    targetScore,
                    session.State.ToString(),
                    session.FailedOrders,
                    session.CompletedOrders,
                    session.PerfectOrders,
                    session.Players.Select(p => new ParticipantResult(
                        p.PlayerId,
                        p.Score,
                        p.OrdersDelivered
                    )).ToList()
                );

                var json = JsonSerializer.Serialize(submission);
                var signature = HmacHelper.ComputeSignature(json, _serverAuthOptions.SharedSecret);

                var url = $"{_gameServerOptions.IdentityApiBaseUrl.TrimEnd('/')}/api/match/result";
                
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Headers.Add("X-Server-Signature", signature);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Match {Id} results submitted successfully.", session.SessionId);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Submission failed for {Id}. Status: {Code}, Error: {Error}", 
                        session.SessionId, response.StatusCode, error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during match submission for {Id}", session.SessionId);
            }
        }
    }
}
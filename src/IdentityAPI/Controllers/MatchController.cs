using KitchenOrchestrator.IdentityAPI.Data;
using KitchenOrchestrator.IdentityAPI.Services;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Models;
using KitchenOrchestrator.Shared.Security.Hashing;
using KitchenOrchestrator.Shared.Security.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace KitchenOrchestrator.IdentityAPI.Controllers
{
    [ApiController]
    [Route("api/match")]
    public class MatchController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ServerAuthOptions _serverAuthOptions;
        private readonly IPlayerService _playerService;
        private readonly ILogger<MatchController> _logger;

        public MatchController(
            AppDbContext db,
            IOptions<ServerAuthOptions> serverAuthOptions,
            IPlayerService playerService,
            ILogger<MatchController> logger)
        {
            _db = db;
            _serverAuthOptions = serverAuthOptions.Value;
            _playerService = playerService;
            _logger = logger;
        }

        [HttpPost("result")]
        public async Task<IActionResult> SubmitResult()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            var signature = Request.Headers[HmacHelper.SignatureHeaderName].FirstOrDefault();

            if (string.IsNullOrEmpty(signature))
            {
                _logger.LogWarning("Match result submission rejected: missing signature header.");
                return Unauthorized(new { error = "Missing signature header." });
            }

            if (!HmacHelper.VerifySignature(rawBody, _serverAuthOptions.SharedSecret, signature))
            {
                _logger.LogWarning("Match result submission rejected: invalid signature.");
                return Unauthorized(new { error = "Invalid signature." });
            }

            MatchResultSubmission? submission;
            try
            {
                submission = JsonSerializer.Deserialize<MatchResultSubmission>(rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize match result submission.");
                return BadRequest(new { error = "Malformed submission payload." });
            }

            if (submission == null)
                return BadRequest(new { error = "Empty submission payload." });

            bool alreadyExists = await _db.MatchHistories
                .AnyAsync(m => m.MatchSessionId == submission.MatchSessionId);

            if (alreadyExists)
            {
                _logger.LogWarning("Duplicate submission ignored for session {SessionId}.", submission.MatchSessionId);
                return Ok(new { status = "already_processed" });
            }

            _logger.LogInformation("Processing match result for session {SessionId}.", submission.MatchSessionId);

            var profiles = new Dictionary<Guid, PlayerProfile>();
            foreach (var participant in submission.Participants)
            {
                var profile = await _db.PlayerProfiles.FindAsync(participant.PlayerProfileId);

                if (profile == null)
                {
                    _logger.LogInformation(
                        "No profile found for {PlayerId} — creating placeholder for dev submission.",
                        participant.PlayerProfileId);

                    profile = new PlayerProfile
                    {
                        Id = participant.PlayerProfileId,
                        SteamId = $"dev-{participant.PlayerProfileId.ToString()[..16]}",
                        DisplayName = $"DevPlayer-{participant.PlayerProfileId.ToString()[..8]}",
                        AccountCreatedUtc = DateTime.UtcNow,
                        LastLoggedInUtc = DateTime.UtcNow
                    };
                    _db.PlayerProfiles.Add(profile);
                    await _db.SaveChangesAsync();
                }

                profiles[participant.PlayerProfileId] = profile;
            }

            bool isWin = submission.FinalScore >= submission.TargetScore &&
                         submission.MatchState == "Completed";

            var matchHistory = new MatchHistory
            {
                MatchSessionId = submission.MatchSessionId,
                MatchBeginUtc = submission.MatchBeginUtc,
                MatchEndUtc = submission.MatchEndUtc,
                LevelId = submission.LevelId,
                FinalScore = submission.FinalScore,
                TargetScore = submission.TargetScore,
                FinalState = isWin ? Shared.Contracts.Enums.MatchState.Completed : Shared.Contracts.Enums.MatchState.Abandoned,
                FailedOrders = submission.FailedOrders,
                CompletedOrders = submission.CompletedOrders,
                PerfectOrders = submission.PerfectOrders
            };

            _db.MatchHistories.Add(matchHistory);
            await _db.SaveChangesAsync();

            foreach (var participant in submission.Participants)
            {
                var matchParticipant = new MatchParticipant
                {
                    MatchHistoryId = matchHistory.Id,
                    PlayerProfileId = participant.PlayerProfileId,
                    IndividualScore = participant.IndividualScore,
                    OrdersDelivered = participant.OrdersDelivered
                };
                _db.MatchParticipants.Add(matchParticipant);

                var profile = profiles[participant.PlayerProfileId];
                profile.MatchesPlayed++;
                profile.TotalScore += participant.IndividualScore;
                if (isWin) profile.MatchesWon++;
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Match {SessionId} persisted. Score: {Score}/{Target}. Win: {IsWin}. Players: {Count}.",
                submission.MatchSessionId, submission.FinalScore, submission.TargetScore,
                isWin, submission.Participants.Count);

            return Ok(new { status = "ok", matchHistoryId = matchHistory.Id });
        }
    }
}
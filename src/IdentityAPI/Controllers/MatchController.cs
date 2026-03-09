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
            // Step 1: Read raw body as string — must happen before any deserialization
            // so HmacHelper can verify against the exact bytes the GameServer signed.
            string rawBody;
            using (var reader = new StreamReader(Request.Body))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            // Step 2: Verify HMAC signature
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

            // Step 3: Deserialize now that the payload is verified
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

            // Step 4: Idempotency guard — reject duplicate submissions for the same session
            bool alreadyExists = await _db.MatchHistories
                .AnyAsync(m => m.MatchSessionId == submission.MatchSessionId);

            if (alreadyExists)
            {
                _logger.LogWarning("Duplicate submission ignored for session {SessionId}.", submission.MatchSessionId);
                return Ok(new { status = "already_processed" });
            }

            _logger.LogInformation("Processing match result for session {SessionId}.", submission.MatchSessionId);

            // Step 5: Ensure all participant profiles exist — upsert dev GUIDs if needed
            var profiles = new Dictionary<Guid, PlayerProfile>();
            foreach (var participant in submission.Participants)
            {
                var profile = await _db.PlayerProfiles.FindAsync(participant.PlayerProfileId);

                if (profile == null)
                {
                    // Dev bypass: create a placeholder profile for mock GUIDs
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

            // Step 6: Determine win condition using IMatchResult.IsWin logic
            bool isWin = submission.FinalScore >= submission.TargetScore &&
                         submission.MatchState == "Completed";

            // Step 7: Create MatchHistory record
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
            await _db.SaveChangesAsync(); // Save to get the generated MatchHistory.Id

            // Step 8: Create MatchParticipant records and update PlayerProfile aggregates
            foreach (var participant in submission.Participants)
            {
                // MatchParticipant
                var matchParticipant = new MatchParticipant
                {
                    MatchHistoryId = matchHistory.Id,
                    PlayerProfileId = participant.PlayerProfileId,
                    IndividualScore = participant.IndividualScore,
                    OrdersDelivered = participant.OrdersDelivered
                };
                _db.MatchParticipants.Add(matchParticipant);

                // PlayerProfile aggregate updates
                var profile = profiles[participant.PlayerProfileId];
                profile.MatchesPlayed++;
                profile.TotalScore += participant.IndividualScore;
                if (isWin) profile.MatchesWon++;

                // PerfectOrders on the profile is a lifetime total — we increment by
                // the session-level PerfectOrders count divided across participants
                // is ambiguous, so we use the individual's OrdersDelivered as a proxy
                // until per-player perfect order tracking is added to ParticipantResult.
                // TODO: add PerfectOrders to ParticipantResult DTO for accurate tracking.
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
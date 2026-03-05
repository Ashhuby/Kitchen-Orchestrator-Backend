// We dont want to send the entire model to the api so we use DTO 
namespace KitchenOrchestrator.Shared.Contracts.DTOs
{
    public record SteamAuthRequest(string SteamSessionTicket, string AppId);
    public record AuthResponse(string Jwt, DateTime TokenExpirationUtc, PlayerProfileDto PlayerProfileDto);
    public record PlayerProfileDto(Guid Id, string DisplayName, int MatchesPlayed, int MatchesWon, int TotalScore, int PerfectOrders);  
    public record MatchResultSubmission(Guid MatchSessionId, DateTime MatchBeginUtc, DateTime MatchEndUtc, string LevelId, int FinalScore, int TargetScore, string MatchState, int FailedOrders, int CompletedOrders, int PerfectOrders, IReadOnlyList<ParticipantResult> Participants);
    public record ParticipantResult(Guid PlayerProfileId, int IndividualScore, int OrdersDelivered); 
    public record  MatchHistorySummaryDto(Guid MatchSessionId, string LevelId, DateTime MatchBeginUtc, DateTime MatchEndUtc, int FinalScore, int TargetScore, bool Won, int FailedOrders, int CompletedOrders, int PerfectOrders, int IndividualScore);
    public record ApiErrorResponse(string Error, string? Detail = null); 
}
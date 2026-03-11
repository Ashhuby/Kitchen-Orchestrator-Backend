// We dont want to send the entire model to the api so we use DTO 
using KitchenOrchestrator.Shared.Contracts.Enums;

namespace KitchenOrchestrator.Shared.Contracts.DTOs
{
    public record SteamAuthRequest(string HexEncodedTicket, string AppId, string DisplayName);
    public record AuthResponse(string Jwt, DateTime TokenExpirationUtc, PlayerProfileDto PlayerProfileDto);
    public record PlayerProfileDto(Guid Id, string DisplayName, int MatchesPlayed, int MatchesWon, int TotalScore, int PerfectOrders);  
    public record MatchResultSubmission(Guid MatchSessionId, DateTime MatchBeginUtc, DateTime MatchEndUtc, string LevelId, int FinalScore, int TargetScore, string MatchState, int FailedOrders, int CompletedOrders, int PerfectOrders, IReadOnlyList<ParticipantResult> Participants);
    public record ParticipantResult(Guid PlayerProfileId, int IndividualScore, int OrdersDelivered); 
    public record MatchHistorySummaryDto(Guid MatchSessionId, string LevelId, DateTime MatchBeginUtc, DateTime MatchEndUtc, int FinalScore, int TargetScore, bool Won, int FailedOrders, int CompletedOrders, int PerfectOrders, int IndividualScore);
    public record ApiErrorResponse(string Error, string? Detail = null); 

    // Lobby
    public record LobbyPlayerDto(Guid PlayerId, string DisplayName, bool IsReady, bool IsHost);
    public record LobbyStateDto(Guid SessionId, string? LevelId, IReadOnlyList<LobbyPlayerDto> Players);
    public record LobbyInfoDto(Guid SessionId, string LobbyName, string HostName, int PlayerCount, int MaxPlayers, string? LevelId);
    public record LobbyCreatedDto(Guid SessionId);

    // Movement — client sends position to server at 10Hz
    public record PositionUpdateDto(Guid SessionId, float X, float Y);

    // Broadcast — server sends full match snapshot to all clients at 10Hz
    public record MatchStateDto(
        Guid SessionId,
        IReadOnlyList<PlayerPositionDto> Players,
        IReadOnlyList<StationStateDto> Stations,
        float TimeRemainingSeconds,
        int TotalScore
    );

    public record PlayerPositionDto(Guid PlayerId, string DisplayName, float X, float Y);

    // Station state — broadcast as part of MatchStateDto
    public record StationStateDto(
        string StationId,
        string StationType,          // matches StationType enum name
        string? HeldIngredient,      // null if empty; matches Ingredient enum name
        string? PrepState,           // null if empty; "Raw" | "Chopped" | "Cooked" | "Burned"
        float ProgressNormalized,    // 0.0 → 1.0, for UI progress bars
        bool IsOccupied              // true if a player is actively chopping at this station
    );

    // Station interaction — client sends this when pressing interact key near a station
    public record StationActionRequest(Guid SessionId, string StationId, StationActionType ActionType);

    // Result of a dish delivery attempt — returned internally by MatchSimulationService
    public record DeliveryResult(bool Success, int Score, bool IsPerfect, string? FailReason);
}
namespace KitchenOrchestrator.Shared.Contracts.DTOs
{
    // ── Auth ──────────────────────────────────────────────────────────────────
    public record SteamAuthRequest(string HexEncodedTicket, string AppId, string DisplayName);
    public record AuthResponse(string Jwt, DateTime TokenExpirationUtc, PlayerProfileDto PlayerProfileDto);
    public record PlayerProfileDto(Guid Id, string DisplayName, int MatchesPlayed, int MatchesWon, int TotalScore, int PerfectOrders);

    // ── Match Persistence ─────────────────────────────────────────────────────
    public record MatchResultSubmission(
        Guid MatchSessionId,
        DateTime MatchBeginUtc,
        DateTime MatchEndUtc,
        string LevelId,
        int FinalScore,
        int TargetScore,
        string MatchState,
        int FailedOrders,
        int CompletedOrders,
        int PerfectOrders,
        IReadOnlyList<ParticipantResult> Participants);

    public record ParticipantResult(Guid PlayerProfileId, int IndividualScore, int OrdersDelivered);

    public record MatchHistorySummaryDto(
        Guid MatchSessionId,
        string LevelId,
        DateTime MatchBeginUtc,
        DateTime MatchEndUtc,
        int FinalScore,
        int TargetScore,
        bool Won,
        int FailedOrders,
        int CompletedOrders,
        int PerfectOrders,
        int IndividualScore);

    public record ApiErrorResponse(string Error, string? Detail = null);

    // ── Lobby List ────────────────────────────────────────────────────────────
    public record LobbyInfoDto(
        Guid SessionId,
        string LobbyName,
        string HostDisplayName,
        int PlayerCount,
        int MaxPlayers,
        string? LevelId);

    public record LobbyCreatedDto(Guid SessionId);

    // ── In-Lobby ──────────────────────────────────────────────────────────────
    public record LobbyPlayerDto(Guid PlayerId, string DisplayName, bool IsReady, bool IsHost);
    public record LobbyStateDto(Guid SessionId, string? LevelId, IReadOnlyList<LobbyPlayerDto> Players);

    // ── Station Layout (host → server on MatchStarted) ────────────────────────
    public record StationLayoutDto(
        string StationId,
        string StationType,
        string? SourceIngredient);

    // ── In-Match: Actions ─────────────────────────────────────────────────────
    public record PositionUpdateDto(Guid SessionId, float X, float Y);

    public record StationActionRequest(
        Guid SessionId,
        string StationId,
        KitchenOrchestrator.Shared.Contracts.Enums.StationActionType ActionType);

    public record DeliveryResult(bool Success, int ScoreAwarded, bool IsPerfect, string? FailureReason);

    // ── In-Match: State Broadcast ─────────────────────────────────────────────
    public record PlayerPositionDto(Guid PlayerId, string DisplayName, float X, float Y);

    public record StationStateDto(
        string StationId,
        string StationType,
        string? HeldIngredient,
        string? PrepState,
        float ProgressNormalized,
        bool IsOccupied);

    public record ActiveOrderDto(
        Guid OrderId,
        string RecipeName,
        IReadOnlyList<string> RequiredIngredients,
        float TimeRemaining,
        float TotalDuration);

    public record MatchPlayerDto(Guid PlayerId, string DisplayName, int Score, int OrdersDelivered);

    public record MatchStateDto(
        Guid SessionId,
        string State,                              // MatchState enum name: "Active", "Completed", "Abandoned"
        IReadOnlyList<PlayerPositionDto> Players,
        IReadOnlyList<StationStateDto> Stations,
        float TimeRemaining,
        int TotalScore);
}
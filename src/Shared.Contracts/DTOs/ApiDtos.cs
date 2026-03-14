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

    // ── Station Layout ────────────────────────────────────────────────────────
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

    /// <summary>
    /// Per-player state. HeldItemType is one of: null, "Ingredient", "Plate".
    /// HeldIngredient is set when HeldItemType == "Ingredient".
    /// HeldPlateContents is set when HeldItemType == "Plate".
    /// </summary>
    public record PlayerPositionDto(
        Guid PlayerId,
        string DisplayName,
        float X,
        float Y,
        string? HeldItemType,                           // null | "Ingredient" | "Plate"
        string? HeldIngredient,                         // Ingredient enum name, set when HeldItemType == "Ingredient"
        IReadOnlyList<string>? HeldPlateContents);      // Ingredient enum names on plate, set when HeldItemType == "Plate"

    /// <summary>
    /// Per-station state. A station holds either an ingredient item or a plate, never both.
    /// HasPlate and PlateContents describe the plate path.
    /// HeldIngredient and PrepState describe the ingredient path.
    /// </summary>
    public record StationStateDto(
        string StationId,
        string StationType,
        string? HeldIngredient,                         // set when station holds a raw/chopped/cooked ingredient
        string? PrepState,                              // ItemPrepState enum name
        float ProgressNormalized,
        bool IsOccupied,
        bool HasPlate,                                  // true when a plate is sitting on this station
        IReadOnlyList<string>? PlateContents);          // ingredient names on the plate, null if no plate

    public record ActiveOrderDto(
        Guid OrderId,
        string RecipeName,
        IReadOnlyList<string> RequiredIngredients,
        float TimeRemaining,
        float TotalDuration);

    public record MatchPlayerDto(Guid PlayerId, string DisplayName, int Score, int OrdersDelivered);

    public record MatchStateDto(
        Guid SessionId,
        string State,
        IReadOnlyList<PlayerPositionDto> Players,
        IReadOnlyList<StationStateDto> Stations,
        float TimeRemaining,
        int TotalScore);
}
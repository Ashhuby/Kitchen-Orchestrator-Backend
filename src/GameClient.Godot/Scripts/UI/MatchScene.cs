using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using System;
using System.Collections.Generic; 
using System.Linq;

/// <summary>
/// Root scene for an active match.
/// Owns the player dictionary and the station dictionary.
/// Receives MatchStateUpdated broadcasts and fans them out to Player and Station nodes.
/// </summary>
public partial class MatchScene : Control
{
    // ── Child nodes ───────────────────────────────────────────────────────────
    private Label _timerLabel = null!;
    private Label _scoreLabel = null!;
    private Node2D _playerContainer = null!;
    private Node2D _stationContainer = null!;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private readonly Dictionary<Guid, Player> _players = new();

    // StationId → Station node — populated from scene tree on _Ready
    private readonly Dictionary<string, Station> _stations = new();

    private MatchStateDto? _pendingState;

    // Packed scene for remote players (local player is instantiated separately)
    [Export] public PackedScene? PlayerScene { get; set; }

    public override void _Ready()
    {
        _timerLabel = GetNode<Label>("HUD/TimerLabel");
        _scoreLabel = GetNode<Label>("HUD/ScoreLabel");
        _playerContainer = GetNode<Node2D>("PlayerContainer");
        _stationContainer = GetNode<Node2D>("StationContainer");

        // Build station lookup from whatever is placed in the scene
        foreach (Node child in _stationContainer.GetChildren())
        {
            if (child is Station station && !string.IsNullOrEmpty(station.StationId))
                _stations[station.StationId] = station;
        }

        // Spawn local player at spawn point
        SpawnLocalPlayer();

        // Subscribe to server events
        Bootstrap.Connection.OnMatchStateUpdated += OnMatchStateUpdated;

        // Report station layout to server (host only — server ignores duplicates)
        if (Bootstrap.State.CurrentSessionId.HasValue)
            CallDeferred(nameof(ReportStationLayout));
    }

    // ── Station Layout Report ─────────────────────────────────────────────────

    private async void ReportStationLayout()
    {
        if (!Bootstrap.State.CurrentSessionId.HasValue) return;

        var layout = _stations.Values.Select(s => new StationLayoutDto(
            s.StationId,
            s.StationType.ToString(),
            string.IsNullOrEmpty(s.SourceIngredient) ? null : s.SourceIngredient
        )).ToList();

        try
        {
            await Bootstrap.Connection.ReportStationLayoutAsync(
                Bootstrap.State.CurrentSessionId.Value, layout);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ReportStationLayout failed: {ex.Message}");
        }
    }

    // ── Match State ───────────────────────────────────────────────────────────

    private void OnMatchStateUpdated(MatchStateDto state)
    {
        _pendingState = state;
        CallDeferred(nameof(ApplyMatchState));
    }

    private void ApplyMatchState()
    {
        if (_pendingState == null) return;
        var state = _pendingState;

        // HUD
        int seconds = (int)state.TimeRemaining;
        _timerLabel.Text = $"{seconds / 60:D2}:{seconds % 60:D2}";
        _scoreLabel.Text = $"Score: {state.TotalScore}";

        // Players
        foreach (var dto in state.Players)
        {
            if (!_players.TryGetValue(dto.PlayerId, out var playerNode))
            {
                playerNode = SpawnRemotePlayer(dto.PlayerId, dto.DisplayName);
                if (playerNode == null) continue;
            }

            if (!playerNode.IsLocalPlayer)
                playerNode.ApplySnapshot(dto.X, dto.Y);
        }

        // Stations
        foreach (var dto in state.Stations)
        {
            if (_stations.TryGetValue(dto.StationId, out var stationNode))
                stationNode.ApplyState(dto);
        }

        // Match end
        if (state.State == "Completed" || state.State == "Abandoned")
            HandleMatchEnd(state);
    }

    // ── Player Spawning ───────────────────────────────────────────────────────

    private void SpawnLocalPlayer()
    {
        if (PlayerScene == null)
        {
            GD.PrintErr("MatchScene: PlayerScene export is not set.");
            return;
        }

        var localPlayerId = Bootstrap.State.PlayerId;
        if (localPlayerId == null) return;

        var player = PlayerScene.Instantiate<Player>();
        _playerContainer.AddChild(player);
        player.Initialise(localPlayerId.Value, Bootstrap.State.Profile!.DisplayName,
            isLocal: true, spawnPosition: new Vector2(200, 200));

        _players[localPlayerId.Value] = player;
    }

    private Player? SpawnRemotePlayer(Guid playerId, string displayName)
    {
        if (PlayerScene == null) return null;

        var player = PlayerScene.Instantiate<Player>();
        _playerContainer.AddChild(player);
        player.Initialise(playerId, displayName,
            isLocal: false, spawnPosition: new Vector2(200, 200));

        _players[playerId] = player;
        return player;
    }

    // ── Match End ─────────────────────────────────────────────────────────────

    private void HandleMatchEnd(MatchStateDto state)
    {
        CleanupSubscriptions();
        GD.Print($"Match over. Final score: {state.TotalScore}");
        // TODO: transition to results screen
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void CleanupSubscriptions()
    {
        Bootstrap.Connection.OnMatchStateUpdated -= OnMatchStateUpdated;
    }

    public override void _ExitTree()
    {
        CleanupSubscriptions();
    }
}
using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Root scene for an active match.
/// Inherits Node2D — NOT Control — so it does not consume keyboard input
/// before Station nodes get a chance to handle it via _UnhandledKeyInput.
/// HUD elements live on a CanvasLayer child and are unaffected by this change.
/// </summary>
public partial class MatchScene : Node2D
{
    // ── Child nodes (always present in MatchScene.tscn) ───────────────────────
    private Label _timerLabel = null!;
    private Label _scoreLabel = null!;

    // ── Set after map is loaded ───────────────────────────────────────────────
    private Node2D _playerContainer = null!;
    private Node2D _stationContainer = null!;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private readonly Dictionary<Guid, Player> _players = new();
    private readonly Dictionary<string, Station> _stations = new();
    private MatchStateDto? _pendingState;

    [Export] public PackedScene? PlayerScene { get; set; }

    private static readonly Dictionary<string, string> LevelScenePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Map0", "res://Scenes/Maps/Map0.tscn" },
        { "Map1", "res://Scenes/Maps/Map1.tscn" },
        { "Map2", "res://Scenes/Maps/Map2.tscn" },
        { "Map3", "res://Scenes/Maps/Map3.tscn" },
    };

    public override void _Ready()
    {
        _timerLabel = GetNode<Label>("HUD/TimerLabel");
        _scoreLabel = GetNode<Label>("HUD/ScoreLabel");

        if (!LoadMap())
        {
            GD.PrintErr("MatchScene: Failed to load map. Aborting match setup.");
            return;
        }

        SpawnLocalPlayer();

        Bootstrap.Connection.OnMatchStateUpdated += OnMatchStateUpdated;

        if (Bootstrap.State.CurrentSessionId.HasValue)
            CallDeferred(nameof(ReportStationLayout));
    }

    // ── Map Loading ───────────────────────────────────────────────────────────

    private bool LoadMap()
    {
        string? levelId = Bootstrap.State.LevelId;

        if (string.IsNullOrEmpty(levelId))
        {
            GD.PrintErr("MatchScene: Bootstrap.State.LevelId is null — cannot load map.");
            return false;
        }

        if (!LevelScenePaths.TryGetValue(levelId, out var scenePath))
        {
            GD.PrintErr($"MatchScene: No scene path registered for LevelId '{levelId}'.");
            return false;
        }

        var packed = GD.Load<PackedScene>(scenePath);
        if (packed == null)
        {
            GD.PrintErr($"MatchScene: Failed to load scene at '{scenePath}'.");
            return false;
        }

        var mapInstance = packed.Instantiate<Node2D>();
        AddChild(mapInstance);
        MoveChild(mapInstance, 0);

        _playerContainer  = mapInstance.GetNodeOrNull<Node2D>("PlayerContainer")
            ?? CreateFallbackContainer(mapInstance, "PlayerContainer");

        _stationContainer = mapInstance.GetNodeOrNull<Node2D>("StationContainer")
            ?? CreateFallbackContainer(mapInstance, "StationContainer");

        foreach (Node child in _stationContainer.GetChildren())
        {
            if (child is Station station && !string.IsNullOrEmpty(station.StationId))
                _stations[station.StationId] = station;
        }

        GD.Print($"MatchScene: Loaded map '{levelId}' with {_stations.Count} stations.");
        return true;
    }

    private Node2D CreateFallbackContainer(Node parent, string name)
    {
        GD.PrintErr($"MatchScene: Map scene missing '{name}' node — creating empty fallback.");
        var node = new Node2D();
        node.Name = name;
        parent.AddChild(node);
        return node;
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

        int seconds = (int)state.TimeRemaining;
        _timerLabel.Text = $"{seconds / 60:D2}:{seconds % 60:D2}";
        _scoreLabel.Text = $"Score: {state.TotalScore}";

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

        foreach (var dto in state.Stations)
        {
            if (_stations.TryGetValue(dto.StationId, out var stationNode))
                stationNode.ApplyState(dto);
        }

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
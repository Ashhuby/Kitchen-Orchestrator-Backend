using Godot;
using System;
using System.Collections.Generic;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;

public partial class MatchScene : Control
{
    // ── Node References ───────────────────────────────────────────────────────

    private Label _statusLabel = null!;
    private Label _timerLabel = null!;
    private Label _scoreLabel = null!;
    private Node2D _worldRoot = null!;  // Parent node for all spawned characters

    // ── Player Tracking ───────────────────────────────────────────────────────

    private readonly Dictionary<Guid, Player> _spawnedPlayers = new();
    private PackedScene _playerScene = null!;

    // ── Latest State (set from SignalR thread, applied on main thread) ─────────

    private MatchStateDto? _latestState;

    // ── Spawn Positions ───────────────────────────────────────────────────────
    // Simple fixed spawn points — enough to get two players in without overlap.
    // Replace with per-map spawn markers once level design is done.

    private static readonly Vector2[] SpawnPositions = new[]
    {
        new Vector2(200f, 300f),
        new Vector2(400f, 300f),
        new Vector2(200f, 500f),
        new Vector2(400f, 500f),
    };

    // ── Godot Lifecycle ───────────────────────────────────────────────────────

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _timerLabel  = GetNode<Label>("VBoxContainer/TimerLabel");
        _scoreLabel  = GetNode<Label>("VBoxContainer/ScoreLabel");
        _worldRoot   = GetNode<Node2D>("World");

        _playerScene = GD.Load<PackedScene>("res://Scenes/Player.tscn");

        _statusLabel.Text = "Match in progress...";
        _timerLabel.Text  = "";
        _scoreLabel.Text  = "Score: 0";

        Bootstrap.Connection.OnMatchStateUpdated += OnMatchStateUpdated;
        Bootstrap.Connection.OnError += OnServerError;
    }

    public override void _ExitTree()
    {
        Bootstrap.Connection.OnMatchStateUpdated -= OnMatchStateUpdated;
        Bootstrap.Connection.OnError -= OnServerError;
    }

    // ── SignalR Callbacks (background thread) ─────────────────────────────────

    private void OnMatchStateUpdated(MatchStateDto state)
    {
        _latestState = state;
        CallDeferred(nameof(ApplyMatchState));
    }

    private void OnServerError(string error)
    {
        GD.PrintErr($"MatchScene server error: {error}");
    }

    // ── Main Thread: Apply State ───────────────────────────────────────────────

    private void ApplyMatchState()
    {
        if (_latestState == null) return;
        var state = _latestState;

        UpdateHUD(state);
        SyncPlayers(state);

        if (state.TimeRemainingSeconds <= 0)
        {
            _statusLabel.Text = $"Match Over! Final Score: {state.TotalScore}";
            Bootstrap.Connection.OnMatchStateUpdated -= OnMatchStateUpdated;
        }
    }

    private void UpdateHUD(MatchStateDto state)
    {
        int seconds = (int)state.TimeRemainingSeconds;
        _timerLabel.Text = $"{seconds / 60:D2}:{seconds % 60:D2}";
        _scoreLabel.Text = $"Score: {state.TotalScore}";
    }

    private void SyncPlayers(MatchStateDto state)
    {
        var localPlayerId = Bootstrap.State.PlayerId;

        int spawnIndex = 0;

        foreach (var playerDto in state.Players)
        {
            if (_spawnedPlayers.TryGetValue(playerDto.PlayerId, out var existing))
            {
                // Already spawned — update remote players via snapshot interpolation.
                // Local player drives its own position via _PhysicsProcess.
                if (!existing.IsLocalPlayer)
                    existing.ApplySnapshot(playerDto.X, playerDto.Y);
            }
            else
            {
                // First time seeing this player — spawn them
                var spawnPos = spawnIndex < SpawnPositions.Length
                    ? SpawnPositions[spawnIndex]
                    : new Vector2(300f + spawnIndex * 60f, 300f);

                bool isLocal = playerDto.PlayerId == localPlayerId;
                SpawnPlayer(playerDto.PlayerId, playerDto.DisplayName, isLocal, spawnPos);
                spawnIndex++;
            }
        }

        // Remove players who left mid-match
        var arrived = new HashSet<Guid>();
        foreach (var p in state.Players) arrived.Add(p.PlayerId);

        foreach (var id in new List<Guid>(_spawnedPlayers.Keys))
        {
            if (!arrived.Contains(id))
            {
                _spawnedPlayers[id].QueueFree();
                _spawnedPlayers.Remove(id);
            }
        }
    }

    private void SpawnPlayer(Guid playerId, string displayName, bool isLocal, Vector2 position)
    {
        if (_playerScene == null)
        {
            GD.PrintErr("MatchScene: _playerScene is null — Player.tscn failed to load.");
            return;
        }

        var instance = _playerScene.Instantiate<Player>();
        _worldRoot.AddChild(instance);
        instance.Initialise(playerId, displayName, isLocal, position);
        _spawnedPlayers[playerId] = instance;

        GD.Print($"Spawned {(isLocal ? "(local)" : "(remote)")} player {displayName} at {position}");
    }
}
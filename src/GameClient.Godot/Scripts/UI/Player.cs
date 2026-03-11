using Godot;
using System;
using System.Collections.Generic;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;

/// <summary>
/// Represents a player character in the match scene.
/// 
/// LOCAL PLAYER:
///   - Reads WASD input each physics frame
///   - Moves via MoveAndSlide (CharacterBody2D collision handled by Godot)
///   - Sends position to server at 10Hz via _positionSendTimer
///   - Detects nearby stations and sends RequestAction on interact key
/// 
/// REMOTE PLAYER:
///   - Never reads input
///   - Receives position snapshots via ApplySnapshot()
///   - Interpolates between the last two received snapshots to avoid teleporting at 10Hz
/// </summary>
public partial class Player : CharacterBody2D
{
    // -------------------------------------------------------------------------
    // Exported — set by MatchScene when spawning
    // -------------------------------------------------------------------------

    [Export] public bool IsLocalPlayer { get; set; } = false;
    [Export] public float MoveSpeed { get; set; } = 200f;

    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------

    public Guid PlayerId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Node references — populated in _Ready
    // -------------------------------------------------------------------------

    private Label _nameLabel = null!;
    private ColorRect _sprite = null!;

    // -------------------------------------------------------------------------
    // Position send timer (local player only)
    // -------------------------------------------------------------------------

    private float _positionSendTimer = 0f;
    private const float PositionSendInterval = 0.1f; // 10Hz

    // -------------------------------------------------------------------------
    // Snapshot interpolation (remote player only)
    //
    // We keep two snapshots: the one we're interpolating FROM and the one we're
    // interpolating TOWARD. When a new snapshot arrives, the current "to" becomes
    // the new "from" and the new snapshot becomes the new "to".
    //
    // InterpolationFactor advances from 0→1 over PositionSendInterval seconds.
    // At 10Hz this means the remote player always renders ~100ms behind real position,
    // which is the minimum needed to have two points to interpolate between.
    // -------------------------------------------------------------------------

    private Vector2 _snapshotFrom = Vector2.Zero;
    private Vector2 _snapshotTo = Vector2.Zero;
    private float _interpolationFactor = 1f; // Start at 1 so we don't interpolate before first snapshot

    // -------------------------------------------------------------------------
    // Station interaction (local player only)
    //
    // MatchScene registers nearby stations by calling SetNearbyStation() when
    // the player enters/exits a station's Area2D. The player then tracks which
    // station it's overlapping so it can send RequestAction on the interact key.
    // -------------------------------------------------------------------------

    private string? _nearbyStationId = null;

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    public void Initialise(Guid playerId, string displayName, bool isLocal, Vector2 spawnPosition)
    {
        PlayerId = playerId;
        DisplayName = displayName;
        IsLocalPlayer = isLocal;
        Position = spawnPosition;

        // Prime both snapshots to spawn position so remote player doesn't interpolate from (0,0)
        _snapshotFrom = spawnPosition;
        _snapshotTo = spawnPosition;
    }

    public override void _Ready()
    {
        _nameLabel = GetNode<Label>("NameLabel");
        _sprite = GetNode<ColorRect>("Sprite");

        _nameLabel.Text = DisplayName + " bleh";

        // Visual distinction: local player is white, remotes are grey
        _sprite.Color = IsLocalPlayer ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
    }

    // -------------------------------------------------------------------------
    // Per-frame update
    // -------------------------------------------------------------------------

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (IsLocalPlayer)
        {
            ProcessLocalMovement(dt);
            ProcessInteractInput();
        }
        else
        {
            ProcessRemoteInterpolation(dt);
        }
    }

    // -------------------------------------------------------------------------
    // Local player: movement + input
    // -------------------------------------------------------------------------

    private void ProcessLocalMovement(float dt)
    {
        Vector2 direction = Vector2.Zero;

        if (Input.IsActionPressed("ui_right")) direction.X += 1f;
        if (Input.IsActionPressed("ui_left")) direction.X -= 1f;
        if (Input.IsActionPressed("ui_down")) direction.Y += 1f;
        if (Input.IsActionPressed("ui_up")) direction.Y -= 1f;

        Velocity = direction.Normalized() * MoveSpeed;
        MoveAndSlide();

        // Send position to server at 10Hz
        _positionSendTimer += dt;
        if (_positionSendTimer >= PositionSendInterval)
        {
            _positionSendTimer = 0f;
            SendPositionToServer();
        }
    }

    private void ProcessInteractInput()
    {
        // "interact" action must be defined in Godot Project Settings → Input Map
        // Default binding: E key
        if (!Input.IsActionJustPressed("interact")) return;
        if (_nearbyStationId == null) return;

        var sessionId = Bootstrap.State.CurrentSessionId;
        if (!sessionId.HasValue) return;

        // Determine action type based on what we're holding and what the station is.
        // The client makes a best-guess here; the server validates and rejects if wrong.
        // TODO: Pass current held item state once client-side held item tracking is in place.
        // For now, always send Pickup — the server will reject if inapplicable.
        var actionRequest = new StationActionRequest(
            sessionId.Value,
            _nearbyStationId,
            KitchenOrchestrator.Shared.Contracts.Enums.StationActionType.Pickup
        );

        // Fire and forget — result comes back via MatchStateDto broadcast
        _ = Bootstrap.Connection.SendActionAsync(actionRequest);
    }

    private void SendPositionToServer()
    {
        var sessionId = Bootstrap.State.CurrentSessionId;
        if (!sessionId.HasValue) return;

        var dto = new PositionUpdateDto(sessionId.Value, Position.X, Position.Y);
        _ = Bootstrap.Connection.SendPositionAsync(dto);
    }

    // -------------------------------------------------------------------------
    // Remote player: snapshot interpolation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by MatchScene when a new MatchStateDto arrives with this player's position.
    /// </summary>
    public void ApplySnapshot(float x, float y)
    {
        _snapshotFrom = Position;         // Start interpolation from where we currently are visually
        _snapshotTo = new Vector2(x, y);
        _interpolationFactor = 0f;        // Reset interpolation — will reach _snapshotTo in one interval
    }

    private void ProcessRemoteInterpolation(float dt)
    {
        if (_interpolationFactor >= 1f) return; // Already at target — no movement needed

        _interpolationFactor = Mathf.Min(_interpolationFactor + dt / PositionSendInterval, 1f);
        Position = _snapshotFrom.Lerp(_snapshotTo, _interpolationFactor);
    }

    // -------------------------------------------------------------------------
    // Station proximity (called by station Area2D signals via MatchScene)
    // -------------------------------------------------------------------------

    public void SetNearbyStation(string? stationId)
    {
        _nearbyStationId = stationId;
    }
}
using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using System;

public partial class Player : CharacterBody2D
{
    // ── Properties ────────────────────────────────────────────────────────────
    public Guid PlayerId { get; private set; }
    public bool IsLocalPlayer { get; private set; }

    /// <summary>
    /// True if this player is currently holding an item.
    /// Updated optimistically by Station.cs immediately when an action is sent,
    /// without waiting for the server round-trip. The server is still authoritative
    /// — if the action is rejected, the next MatchStateDto will correct this.
    /// </summary>
    public bool HasHeldItem { get; private set; }

    // ── Child nodes ───────────────────────────────────────────────────────────
    private ColorRect _sprite = null!;
    private Label _nameLabel = null!;

    // ── Movement ──────────────────────────────────────────────────────────────
    private const float Speed = 200f;
    private const float PositionSendIntervalSec = 0.1f;
    private float _positionSendTimer = 0f;

    // ── Interpolation (remote players) ────────────────────────────────────────
    private Vector2 _targetPosition;
    private const float InterpolationSpeed = 15f;

    public override void _Ready()
    {
        _sprite    = GetNode<ColorRect>("Sprite");
        _nameLabel = GetNode<Label>("NameLabel");
    }

    public void Initialise(Guid playerId, string displayName, bool isLocal, Vector2 spawnPosition)
    {
        PlayerId        = playerId;
        IsLocalPlayer   = isLocal;
        Position        = spawnPosition;
        _targetPosition = spawnPosition;
        _nameLabel.Text = displayName;

        if (isLocal)
            _sprite.Color = new Color(0.2f, 0.6f, 1.0f); // Blue
        else
            _sprite.Color = new Color(1.0f, 0.4f, 0.2f); // Orange
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsLocalPlayer)
            HandleLocalMovement((float)delta);
        else
            HandleRemoteInterpolation((float)delta);
    }

    private void HandleLocalMovement(float delta)
    {
        var dir = Vector2.Zero;
        if (Input.IsActionPressed("ui_right")) dir.X += 1;
        if (Input.IsActionPressed("ui_left"))  dir.X -= 1;
        if (Input.IsActionPressed("ui_down"))  dir.Y += 1;
        if (Input.IsActionPressed("ui_up"))    dir.Y -= 1;

        Velocity = dir.Normalized() * Speed;
        MoveAndSlide();

        _positionSendTimer += delta;
        if (_positionSendTimer >= PositionSendIntervalSec)
        {
            _positionSendTimer = 0f;
            var sessionId = Bootstrap.State.CurrentSessionId;
            if (sessionId.HasValue)
            {
                Bootstrap.Connection.SendPositionAsync(new PositionUpdateDto(
                    sessionId.Value, Position.X, Position.Y));
            }
        }
    }

    private void HandleRemoteInterpolation(float delta)
    {
        Position = Position.Lerp(_targetPosition, InterpolationSpeed * delta);
    }

    public void ApplySnapshot(float x, float y)
    {
        _targetPosition = new Vector2(x, y);
    }

    /// <summary>
    /// Called by Station.cs immediately after sending an action to the server.
    /// Keeps the local player's held item state in sync optimistically so that
    /// ResolveAction() on the next station gives the correct result without
    /// waiting for the server's MatchStateDto broadcast.
    /// </summary>
    public void SetHeldItem(bool hasItem)
    {
        HasHeldItem = hasItem;
        // TODO: show/hide held item visual on player sprite
    }
}
using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using System;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
    public Guid PlayerId { get; private set; }
    public bool IsLocalPlayer { get; private set; }

    public bool HasHeldItem { get; private set; }
    public bool HasPlate { get; private set; }
    public List<string> PlateContents { get; private set; } = new();

    private ColorRect _sprite = null!;
    private Label _nameLabel = null!;
    private Label _heldItemLabel = null!;

    private const float Speed = 200f;
    private const float PositionSendIntervalSec = 0.1f;
    private float _positionSendTimer = 0f;

    private Vector2 _targetPosition;
    private const float InterpolationSpeed = 15f;

    public override void _Ready()
    {
        _sprite        = GetNode<ColorRect>("Sprite");
        _nameLabel     = GetNode<Label>("NameLabel");
        _heldItemLabel = GetNode<Label>("HeldItemLabel");

        _heldItemLabel.Text = "";
    }

    public void Initialise(Guid playerId, string displayName, bool isLocal, Vector2 spawnPosition)
    {
        PlayerId        = playerId;
        IsLocalPlayer   = isLocal;
        Position        = spawnPosition;
        _targetPosition = spawnPosition;
        _nameLabel.Text = displayName;

        _sprite.Color = isLocal
            ? new Color(0.2f, 0.6f, 1.0f)
            : new Color(1.0f, 0.4f, 0.2f);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsLocalPlayer) HandleLocalMovement((float)delta);
        else HandleRemoteInterpolation((float)delta);
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
                Bootstrap.Connection.SendPositionAsync(
                    new PositionUpdateDto(sessionId.Value, Position.X, Position.Y));
        }
    }

    private void HandleRemoteInterpolation(float delta)
    {
        Position = Position.Lerp(_targetPosition, InterpolationSpeed * delta);
    }

    public void ApplySnapshot(float x, float y) => _targetPosition = new Vector2(x, y);

    // ── Optimistic held state ─────────────────────────────────────────────────

    public void SetHeldIngredient(bool hasItem)
    {
        HasHeldItem = hasItem;
        if (hasItem) { HasPlate = false; PlateContents = new List<string>(); }
        UpdateHeldItemLabel();
    }

    public void SetHeldPlate(bool hasPlate, List<string>? contents = null)
    {
        HasPlate      = hasPlate;
        PlateContents = hasPlate ? (contents ?? new List<string>()) : new List<string>();
        if (hasPlate) HasHeldItem = false;
        UpdateHeldItemLabel();
    }

    // Legacy compatibility
    public void SetHeldItem(bool hasItem) => SetHeldIngredient(hasItem);

    // ── Authoritative state from server ──────────────────────────────────────

    public void ApplyAuthoritative(PlayerPositionDto dto)
    {
        if (!IsLocalPlayer)
            ApplySnapshot(dto.X, dto.Y);

        switch (dto.HeldItemType)
        {
            case "Ingredient":
                HasHeldItem   = true;
                HasPlate      = false;
                PlateContents = new List<string>();
                break;
            case "Plate":
                HasPlate      = true;
                HasHeldItem   = false;
                PlateContents = dto.HeldPlateContents != null
                    ? new List<string>(dto.HeldPlateContents)
                    : new List<string>();
                break;
            default:
                HasHeldItem   = false;
                HasPlate      = false;
                PlateContents = new List<string>();
                break;
        }
        GD.Print($"[Player] ApplyAuthoritative: HeldItemType={dto.HeldItemType} IsLocal={IsLocalPlayer}");
        UpdateHeldItemLabel();
    }

    // ── Debug visual ──────────────────────────────────────────────────────────

    private void UpdateHeldItemLabel()
    {
        if (HasPlate)
        {
            string contents = PlateContents.Count > 0
                ? string.Join("+", PlateContents)
                : "empty";
            _heldItemLabel.Text      = $"[{contents}]";
            _heldItemLabel.Modulate  = new Color(1f, 0.9f, 0.2f); // yellow for plate
        }
        else if (HasHeldItem)
        {
            _heldItemLabel.Text     = "[item]";
            _heldItemLabel.Modulate = new Color(0.4f, 1f, 0.4f); // green for ingredient
        }
        else
        {
            _heldItemLabel.Text = "";
        }
    }
}
using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;

/// <summary>
/// Attach to a Station node in any map scene.
/// Set StationId and StationType in the inspector.
/// The node registers itself with MatchScene on _Ready so MatchScene can push
/// StationStateDto updates to it without needing to know the scene tree structure.
/// </summary>
public partial class Station : Area2D
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Export] public string StationId { get; set; } = string.Empty;
    [Export] public StationType StationType { get; set; } = StationType.Counter;

    // Only required for IngredientSource stations
    [Export] public string SourceIngredient { get; set; } = string.Empty;

    // ── Child nodes (set in Station.tscn) ────────────────────────────────────
    private Sprite2D _sprite = null!;
    private Label _itemLabel = null!;
    private ProgressBar _progressBar = null!;
    private Label _actionHint = null!;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private bool _playerInRange = false;
    private Player? _localPlayer;
    private StationStateDto? _lastKnownState;

    // ── Interaction radius (pixels, must match CollisionShape2D in .tscn) ─────
    private const float InteractionRadius = 96f;

    public override void _Ready()
    {
        _sprite = GetNode<Sprite2D>("Sprite2D");
        _itemLabel = GetNode<Label>("ItemLabel");
        _progressBar = GetNode<ProgressBar>("ProgressBar");
        _actionHint = GetNode<Label>("ActionHint");

        _progressBar.Visible = false;
        _actionHint.Visible = false;
        _itemLabel.Text = string.Empty;

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _Input(InputEvent @event)
    {
        if (!_playerInRange) return;
        if (_localPlayer == null) return;
        if (!@event.IsActionPressed("interact")) return;

        var action = ResolveAction();
        if (action == null) return;

        var sessionId = Bootstrap.State.CurrentSessionId;
        if (!sessionId.HasValue) return;

        Bootstrap.Connection.SendActionAsync(new StationActionRequest(
            sessionId.Value,
            StationId,
            action.Value));
    }

    // ── State update (called by MatchScene when MatchStateUpdated arrives) ────

    public void ApplyState(StationStateDto state)
    {
        _lastKnownState = state;

        // Item label
        _itemLabel.Text = state.HeldIngredient != null
            ? $"{state.HeldIngredient} ({state.PrepState})"
            : string.Empty;

        // Progress bar — show for chopping and cooking
        bool isProcessing = state.ProgressNormalized > 0f && state.ProgressNormalized < 1f;
        _progressBar.Visible = isProcessing;
        _progressBar.Value = state.ProgressNormalized * 100.0;

        // Action hint
        if (_playerInRange)
            UpdateActionHint();
    }

    // ── Proximity ─────────────────────────────────────────────────────────────

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player player) return;
        if (!player.IsLocalPlayer) return;

        _localPlayer = player;
        _playerInRange = true;
        UpdateActionHint();
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not Player player) return;
        if (!player.IsLocalPlayer) return;

        // If player walks away from a ChoppingBoard mid-prep, cancel it
        if (StationType == StationType.ChoppingBoard &&
            _lastKnownState?.IsOccupied == true)
        {
            var sessionId = Bootstrap.State.CurrentSessionId;
            if (sessionId.HasValue)
            {
                Bootstrap.Connection.SendActionAsync(new StationActionRequest(
                    sessionId.Value,
                    StationId,
                    StationActionType.CancelPrep));
            }
        }

        _localPlayer = null;
        _playerInRange = false;
        _actionHint.Visible = false;
    }

    // ── Context-sensitive action resolution ───────────────────────────────────

    /// <summary>
    /// Determines the correct action to send when the player presses E.
    /// Returns null if no valid action exists (so we don't send noise to the server).
    /// </summary>
    private StationActionType? ResolveAction()
    {
        if (_localPlayer == null) return null;

        bool playerHasItem = _localPlayer.HasHeldItem;
        bool stationHasItem = _lastKnownState?.HeldIngredient != null;
        bool stationComplete = _lastKnownState?.ProgressNormalized >= 1f;
        bool stationOccupied = _lastKnownState?.IsOccupied ?? false;

        return StationType switch
        {
            StationType.IngredientSource =>
                !playerHasItem ? StationActionType.Pickup : null,

            StationType.Counter =>
                playerHasItem && !stationHasItem ? StationActionType.Deposit :
                !playerHasItem && stationHasItem ? StationActionType.Collect :
                null,

            StationType.ChoppingBoard =>
                stationComplete && !playerHasItem ? StationActionType.Collect :
                playerHasItem && !stationHasItem ? StationActionType.Deposit :
                stationHasItem && !stationComplete && !stationOccupied && !playerHasItem ? StationActionType.BeginPrep :
                null,

            StationType.Stove =>
                stationComplete && !playerHasItem ? StationActionType.Collect :
                playerHasItem && !stationHasItem ? StationActionType.Deposit :
                null,

            StationType.DeliveryCounter =>
                playerHasItem ? StationActionType.Deliver : null,

            _ => null
        };
    }

    private void UpdateActionHint()
    {
        var action = ResolveAction();
        if (action == null)
        {
            _actionHint.Visible = false;
            return;
        }

        _actionHint.Text = action switch
        {
            StationActionType.Pickup => "[E] Pick up",
            StationActionType.Deposit => "[E] Place",
            StationActionType.BeginPrep => "[E] Chop",
            StationActionType.CancelPrep => "[E] Cancel",
            StationActionType.Collect => "[E] Collect",
            StationActionType.Deliver => "[E] Deliver",
            _ => "[E]"
        };
        _actionHint.Visible = true;
    }
}
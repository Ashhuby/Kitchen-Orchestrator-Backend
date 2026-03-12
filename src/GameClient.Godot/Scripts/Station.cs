using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;

public partial class Station : Area2D
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Export] public string StationId { get; set; } = string.Empty;
    [Export] public StationType StationType { get; set; } = StationType.Counter;
    [Export] public string SourceIngredient { get; set; } = string.Empty;

    // ── Child nodes ───────────────────────────────────────────────────────────
    private Label _itemLabel = null!;
    private ProgressBar _progressBar = null!;
    private Label _actionHint = null!;

    // ── Runtime state ─────────────────────────────────────────────────────────
    private bool _playerInRange = false;
    private Player? _localPlayer;
    private StationStateDto? _lastKnownState;

    public override void _Ready()
    {
        _itemLabel   = GetNode<Label>("ItemLabel");
        _progressBar = GetNode<ProgressBar>("ProgressBar");
        _actionHint  = GetNode<Label>("ActionHint");

        _progressBar.Visible = false;
        _actionHint.Visible  = false;
        _itemLabel.Text      = string.Empty;

        SetProcessUnhandledKeyInput(true);

        BodyEntered += OnBodyEntered;
        BodyExited  += OnBodyExited;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!_playerInRange) return;
        if (_localPlayer == null) return;
        if (!@event.IsActionPressed("interact")) return;

        var action = ResolveAction();
        GD.Print($"[Station {StationId}] E pressed. Resolved action: {action?.ToString() ?? "null"}");

        if (action == null) return;

        var sessionId = Bootstrap.State.CurrentSessionId;
        if (!sessionId.HasValue)
        {
            GD.PrintErr($"[Station {StationId}] No CurrentSessionId — cannot send action.");
            return;
        }

        Bootstrap.Connection.SendActionAsync(new StationActionRequest(
            sessionId.Value,
            StationId,
            action.Value));

        GetViewport().SetInputAsHandled();
    }

    // ── State update ──────────────────────────────────────────────────────────

    public void ApplyState(StationStateDto state)
    {
        _lastKnownState = state;

        _itemLabel.Text = state.HeldIngredient != null
            ? $"{state.HeldIngredient} ({state.PrepState})"
            : string.Empty;

        bool isActive = state.ProgressNormalized > 0f && state.ProgressNormalized < 1f;
        _progressBar.Visible = isActive;
        _progressBar.Value   = state.ProgressNormalized * 100.0;

        if (_playerInRange)
            UpdateActionHint();
    }

    // ── Proximity ─────────────────────────────────────────────────────────────

    private void OnBodyEntered(Node2D body)
    {
        if (body is not Player player) return;
        if (!player.IsLocalPlayer) return;

        _localPlayer   = player;
        _playerInRange = true;
        GD.Print($"[Station {StationId}] Player entered range.");
        UpdateActionHint();
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is not Player player) return;
        if (!player.IsLocalPlayer) return;

        if (StationType == StationType.ChoppingBoard && _lastKnownState?.IsOccupied == true)
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

        _localPlayer        = null;
        _playerInRange      = false;
        _actionHint.Visible = false;
        GD.Print($"[Station {StationId}] Player left range.");
    }

    // ── Action resolution ─────────────────────────────────────────────────────

    private StationActionType? ResolveAction()
    {
        if (_localPlayer == null) return null;

        bool playerHasItem   = _localPlayer.HasHeldItem;
        bool stationHasItem  = _lastKnownState?.HeldIngredient != null;
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
                stationComplete && !playerHasItem                                         ? StationActionType.Collect  :
                playerHasItem && !stationHasItem                                          ? StationActionType.Deposit  :
                stationHasItem && !stationComplete && !stationOccupied && !playerHasItem  ? StationActionType.BeginPrep :
                null,

            StationType.Stove =>
                stationComplete && !playerHasItem ? StationActionType.Collect :
                playerHasItem && !stationHasItem  ? StationActionType.Deposit :
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
            StationActionType.Pickup    => "[E] Pick up",
            StationActionType.Deposit   => "[E] Place",
            StationActionType.BeginPrep => "[E] Chop",
            StationActionType.CancelPrep => "[E] Cancel",
            StationActionType.Collect   => "[E] Collect",
            StationActionType.Deliver   => "[E] Deliver",
            _ => "[E]"
        };
        _actionHint.Visible = true;
    }
}
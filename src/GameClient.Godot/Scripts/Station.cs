using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;

public partial class Station : Area2D
{
    [Export] public string StationId { get; set; } = string.Empty;
    [Export] public StationType StationType { get; set; } = StationType.Counter;
    [Export] public string SourceIngredient { get; set; } = string.Empty;

    private ColorRect _background = null!;
    private Label _typeLabel = null!;
    private Label _itemLabel = null!;
    private ProgressBar _progressBar = null!;
    private Label _actionHint = null!;

    private bool _playerInRange = false;
    private Player? _localPlayer;
    private StationStateDto? _lastKnownState;

    // Debug colours per station type
    private static Color ColorFor(StationType t) => t switch
    {
        StationType.IngredientSource => new Color(0.2f, 0.6f, 0.2f),   // green
        StationType.PlateSource      => new Color(0.6f, 0.6f, 0.6f),   // grey
        StationType.ChoppingBoard    => new Color(0.6f, 0.5f, 0.2f),   // brown
        StationType.Stove            => new Color(0.7f, 0.2f, 0.1f),   // red
        StationType.Counter          => new Color(0.3f, 0.3f, 0.5f),   // blue-grey
        StationType.DeliveryCounter  => new Color(0.6f, 0.2f, 0.6f),   // purple
        _                            => new Color(0.2f, 0.2f, 0.2f)
    };

    public override void _Ready()
    {
        _background  = GetNode<ColorRect>("Background");
        _typeLabel   = GetNode<Label>("TypeLabel");
        _itemLabel   = GetNode<Label>("ItemLabel");
        _progressBar = GetNode<ProgressBar>("ProgressBar");
        _actionHint  = GetNode<Label>("ActionHint");

        _progressBar.Visible = false;
        _actionHint.Visible  = false;
        _itemLabel.Text      = string.Empty;

        // Set background colour and type label once — they never change
        _background.Color = ColorFor(StationType);
        _typeLabel.Text   = StationType switch
        {
            StationType.IngredientSource => $"SRC\n{SourceIngredient}",
            StationType.PlateSource      => "PLATES",
            StationType.ChoppingBoard    => "CHOP",
            StationType.Stove            => "STOVE",
            StationType.Counter          => "COUNTER",
            StationType.DeliveryCounter  => "DELIVER",
            _ => StationType.ToString()
        };

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
            sessionId.Value, StationId, action.Value));

        ApplyOptimisticState(action.Value);
        UpdateActionHint();
        GetViewport().SetInputAsHandled();
    }

    // ── State update ──────────────────────────────────────────────────────────

    public void ApplyState(StationStateDto state)
    {
        _lastKnownState = state;

        if (state.HasPlate)
        {
            var contents = state.PlateContents != null && state.PlateContents.Count > 0
                ? string.Join("+", state.PlateContents)
                : "empty";
            _itemLabel.Text = $"[{contents}]";
        }
        else if (state.HeldIngredient != null)
        {
            _itemLabel.Text = $"{state.HeldIngredient}\n({state.PrepState})";
        }
        else
        {
            _itemLabel.Text = string.Empty;
        }

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
                    sessionId.Value, StationId, StationActionType.CancelPrep));
            }
        }

        _localPlayer        = null;
        _playerInRange      = false;
        _actionHint.Visible = false;
        GD.Print($"[Station {StationId}] Player left range.");
    }

    // ── Optimistic state ──────────────────────────────────────────────────────

    private void ApplyOptimisticState(StationActionType action)
    {
        if (_localPlayer == null) return;

        switch (action)
        {
            case StationActionType.Pickup:
                if (StationType == StationType.PlateSource)
                    _localPlayer.SetHeldPlate(true);
                else
                    _localPlayer.SetHeldIngredient(true);
                break;

            case StationActionType.Collect:
                bool collectingPlate = _lastKnownState?.HasPlate == true;
                if (collectingPlate)
                    _localPlayer.SetHeldPlate(true, _lastKnownState?.PlateContents != null
                        ? new System.Collections.Generic.List<string>(_lastKnownState.PlateContents)
                        : null);
                else
                    _localPlayer.SetHeldIngredient(true);
                break;

            case StationActionType.Deposit:
            case StationActionType.Deliver:
                _localPlayer.SetHeldIngredient(false);
                _localPlayer.SetHeldPlate(false);
                break;

            case StationActionType.AddToPlate:
                _localPlayer.SetHeldIngredient(false);
                break;
        }
    }

    // ── Action resolution ─────────────────────────────────────────────────────

    private StationActionType? ResolveAction()
    {
        if (_localPlayer == null) return null;

        bool playerHasIngredient  = _localPlayer.HasHeldItem;
        bool playerHasPlate       = _localPlayer.HasPlate;
        bool playerHasAnything    = playerHasIngredient || playerHasPlate;

        bool stationHasIngredient = _lastKnownState?.HeldIngredient != null;
        bool stationHasPlate      = _lastKnownState?.HasPlate == true;
        bool stationHasAnything   = stationHasIngredient || stationHasPlate;

        bool stationComplete  = _lastKnownState?.ProgressNormalized >= 1f;
        bool stationOccupied  = _lastKnownState?.IsOccupied ?? false;

        return StationType switch
        {
            StationType.IngredientSource =>
                !playerHasAnything ? StationActionType.Pickup : null,

            StationType.PlateSource =>
                !playerHasAnything ? StationActionType.Pickup : null,

            StationType.Counter =>
                playerHasIngredient && stationHasPlate   ? StationActionType.AddToPlate :
                playerHasAnything && !stationHasAnything ? StationActionType.Deposit    :
                !playerHasAnything && stationHasAnything ? StationActionType.Collect    :
                null,

            StationType.ChoppingBoard =>
                stationComplete && !playerHasAnything                                              ? StationActionType.Collect  :
                playerHasIngredient && !stationHasAnything                                         ? StationActionType.Deposit  :
                stationHasIngredient && !stationComplete && !stationOccupied && !playerHasAnything ? StationActionType.BeginPrep :
                null,

            StationType.Stove =>
                stationComplete && !playerHasAnything      ? StationActionType.Collect :
                playerHasIngredient && !stationHasAnything ? StationActionType.Deposit :
                null,

            StationType.DeliveryCounter =>
                playerHasPlate ? StationActionType.Deliver : null,

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
            StationActionType.Pickup     => "[E] Pick up",
            StationActionType.Deposit    => "[E] Place",
            StationActionType.BeginPrep  => "[E] Chop",
            StationActionType.CancelPrep => "[E] Cancel",
            StationActionType.Collect    => "[E] Collect",
            StationActionType.Deliver    => "[E] Deliver",
            StationActionType.AddToPlate => "[E] Add to plate",
            _ => "[E]"
        };
        _actionHint.Visible = true;
    }
}
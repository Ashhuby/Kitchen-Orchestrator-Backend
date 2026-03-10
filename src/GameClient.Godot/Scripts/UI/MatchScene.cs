using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;

public partial class MatchScene : Control
{
    private Label _statusLabel = null!;
    private Label _timerLabel = null!;
    private Label _scoreLabel = null!;
    private VBoxContainer _orderListContainer = null!;

    private MatchStateDto? _latestMatchState;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _timerLabel = GetNode<Label>("VBoxContainer/TimerLabel");
        _scoreLabel = GetNode<Label>("VBoxContainer/ScoreLabel");
        _orderListContainer = GetNode<VBoxContainer>("VBoxContainer/OrderListContainer");

        _statusLabel.Text = "Match in progress...";
        _timerLabel.Text = "";
        _scoreLabel.Text = "Score: 0";

        Bootstrap.Connection.OnMatchStateUpdated += OnMatchStateUpdated;
    }

    private void OnMatchStateUpdated(MatchStateDto matchState)
    {
        _latestMatchState = matchState;
        CallDeferred(nameof(ApplyMatchState));
    }

    private void ApplyMatchState()
    {
        if (_latestMatchState == null) return;
        var state = _latestMatchState;

        // Timer
        int seconds = (int)state.TimeRemaining;
        _timerLabel.Text = $"{seconds / 60:D2}:{seconds % 60:D2}";

        // Score
        _scoreLabel.Text = $"Score: {state.TotalScore}";

        // Orders
        foreach (Node child in _orderListContainer.GetChildren())
            child.QueueFree();

        foreach (var order in state.ActiveOrders)
        {
            var label = new Label();
            label.Text = $"{order.RecipeName} — {(int)order.TimeRemaining}s — [{string.Join(", ", order.RequiredIngredients)}]";
            _orderListContainer.AddChild(label);
        }

        // Player scores
        foreach (var player in state.Players)
        {
            var label = new Label();
            label.Text = $"{player.DisplayName}: {player.Score} pts ({player.OrdersDelivered} delivered)";
            _orderListContainer.AddChild(label);
        }

        // Match over
        if (state.State == "Completed" || state.State == "Abandoned")
        {
            CleanupSubscriptions();
            _statusLabel.Text = $"Match Over! Final Score: {state.TotalScore}";
        }
    }

    private void CleanupSubscriptions()
    {
        Bootstrap.Connection.OnMatchStateUpdated -= OnMatchStateUpdated;
    }

    public override void _ExitTree()
    {
        CleanupSubscriptions();
    }
}
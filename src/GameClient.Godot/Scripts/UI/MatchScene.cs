using Godot;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;	
using System.Linq;

public partial class MatchScene : Control
{
    private Label _statusLabel = null!;
    private Label _timerLabel = null!;
    private Label _scoreLabel = null!;
    private VBoxContainer _orderListContainer = null!;

    private MatchStateDto? _latestState;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _timerLabel = GetNode<Label>("VBoxContainer/TimerLabel");
        _scoreLabel = GetNode<Label>("VBoxContainer/ScoreLabel");
        _orderListContainer = GetNode<VBoxContainer>("VBoxContainer/OrderListContainer");

        // Initial UI state
        _statusLabel.Text = "Match in progress...";
        _timerLabel.Text = "02:00";
        _scoreLabel.Text = "Score: 0";

        // Subscribe to live state broadcasts from the server
        Bootstrap.Connection.OnMatchStateUpdated += OnMatchStateUpdated;
    }

    private void OnMatchStateUpdated(MatchStateDto state)
    {
        // Store latest state and defer UI update to Godot's main thread.
        // SignalR callbacks arrive on a background thread — touching Godot
        // nodes directly from here will crash. CallDeferred queues the call
        // safely onto the next frame on the main thread.
        _latestState = state;
        CallDeferred(nameof(ApplyMatchState));
    }

    private void ApplyMatchState()
    {
        if (_latestState == null) return;
        var state = _latestState;

        // Timer — format as MM:SS
        int totalSeconds = (int)state.TimeRemaining;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _timerLabel.Text = $"{minutes:D2}:{seconds:D2}";

        // Score
        _scoreLabel.Text = $"Score: {state.TotalScore}";

        // Active orders — rebuild the list each update
        foreach (Node child in _orderListContainer.GetChildren())
            child.QueueFree();

        foreach (var order in state.ActiveOrders)
        {
            var label = new Label();
            int orderSeconds = (int)order.TimeRemaining;
            string ingredients = string.Join(", ", order.RequiredIngredients);
            label.Text = $"{order.RecipeName} [{ingredients}] — {orderSeconds}s";
            _orderListContainer.AddChild(label);
        }

        // Player scores
        _statusLabel.Text = string.Join("  |  ",
            state.Players.Select(p => $"{p.DisplayName}: {p.Score}"));

        // Match ended — server sets State to "Completed" on the final broadcast
        if (state.State == "Completed")
        {
            CleanupSubscriptions();
            // TODO: Transition to results screen in a future step For now, show final score so the match doesn't just freeze
            _timerLabel.Text = "00:00";
            _statusLabel.Text = $"Match Over! Final Score: {state.TotalScore}";
        }
    }

    private void CleanupSubscriptions()
    {
        Bootstrap.Connection.OnMatchStateUpdated -= OnMatchStateUpdated;
    }

    public override void _ExitTree()
    {
        // Guard against double-unsubscribe if CleanupSubscriptions already ran
        CleanupSubscriptions();
    }
}
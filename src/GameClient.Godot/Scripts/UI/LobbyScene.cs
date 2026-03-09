using Godot;
using System;
using System.Linq;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;

public partial class LobbyScene : Control
{
    private Label _statusLabel = null!;
    private VBoxContainer _playerListContainer = null!;
    private OptionButton _mapOptionButton = null!;
    private Button _readyButton = null!;
    private Button _leaveButton = null!;

    private bool _isHost = false;
    private LobbyStateDto? _latestLobbyState; // Field to hold state for thread-safe UI updates

    public override void _Ready()
    {
        // Get Node References
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _playerListContainer = GetNode<VBoxContainer>("VBoxContainer/PlayerListContainer");
        _mapOptionButton = GetNode<OptionButton>("VBoxContainer/MapOptionButton");
        _readyButton = GetNode<Button>("VBoxContainer/ReadyButton");
        _leaveButton = GetNode<Button>("VBoxContainer/LeaveButton");

        // Initial State
        _readyButton.Disabled = true;
        _mapOptionButton.Disabled = true;
        _statusLabel.Text = "Connected to Server";

        // Setup Map Options
        _mapOptionButton.Clear();
        _mapOptionButton.AddItem("Salad Bar", 0);   
        _mapOptionButton.AddItem("Sushi Bar", 1);   
        _mapOptionButton.AddItem("Burger Diner", 2); 

        // Wire Signals
        _readyButton.Pressed += OnReadyPressed;
        _leaveButton.Pressed += OnLeavePressed;
        _mapOptionButton.ItemSelected += OnMapChanged;

        // Subscribe to SignalR events
        Bootstrap.Connection.OnLobbyStateUpdated += OnLobbyStateUpdated;
        Bootstrap.Connection.OnMatchStarted += OnMatchStarted;
        
        if (Bootstrap.State.CurrentSessionId.HasValue)
        {
            _readyButton.Disabled = false;
        }
    }

    private async void OnReadyPressed()
    {
        if (Bootstrap.State.CurrentSessionId.HasValue)
        {
            await Bootstrap.Connection.SendReadyAsync(Bootstrap.State.CurrentSessionId.Value);
            _readyButton.Disabled = true;
            _statusLabel.Text = "Ready! Waiting for others...";
        }
    }

    private async void OnLeavePressed()
    {
        CleanupSubscriptions();
        await Bootstrap.Connection.DisconnectAsync();
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenuScene.tscn");
    }

    private async void OnMapChanged(long index)
    {
        if (!_isHost || !Bootstrap.State.CurrentSessionId.HasValue) return;

        string levelId = index switch
        {
            0 => "map1",
            1 => "map2",
            2 => "map3",
            _ => "map1"
        };

        await Bootstrap.Connection.ChangeMapAsync(Bootstrap.State.CurrentSessionId.Value, levelId);
    }

    private void OnLobbyStateUpdated(LobbyStateDto lobbyState)
    {
        // Store the state and defer the UI update to the main thread
        _latestLobbyState = lobbyState;
        CallDeferred(nameof(UpdateUIFromLobbyState));
    }

    private void UpdateUIFromLobbyState()
    {
        if (_latestLobbyState == null) return;
        var lobbyState = _latestLobbyState;

        // 1. Determine Host Status (Uses the PlayerId convenience property in ClientState)
        var me = lobbyState.Players.FirstOrDefault(p => p.PlayerId == Bootstrap.State.PlayerId);
        _isHost = me?.IsHost ?? false;

        // 2. Update Map Selector
        _mapOptionButton.Disabled = !_isHost;
        
        int mapIndex = lobbyState.LevelId switch
        {
            "map1" => 0,
            "map2" => 1,
            "map3" => 2,
            _ => 0
        };
        
        if (_mapOptionButton.Selected != mapIndex)
        {
            _mapOptionButton.Select(mapIndex);
        }

        // 3. Rebuild Player List UI
        foreach (Node child in _playerListContainer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var player in lobbyState.Players)
        {
            var label = new Label();
            string hostTag = player.IsHost ? " [HOST]" : "";
            string readyTag = player.IsReady ? " (READY)" : " (Joining...)";
            label.Text = $"{player.DisplayName}{hostTag}{readyTag}";
            _playerListContainer.AddChild(label);
        }

        _statusLabel.Text = _isHost ? "You are the Host" : "Waiting for Host...";
    }

    private void OnMatchStarted(Guid sessionId)
    {
        CallDeferred(nameof(TransitionToMatch));
    }

    private void TransitionToMatch()
    {
        CleanupSubscriptions();
        GetTree().ChangeSceneToFile("res://Scenes/UI/MatchScene.tscn");
    }

    private void CleanupSubscriptions()
    {
        Bootstrap.Connection.OnLobbyStateUpdated -= OnLobbyStateUpdated;
        Bootstrap.Connection.OnMatchStarted -= OnMatchStarted;
    }

    public override void _ExitTree()
    {
        CleanupSubscriptions();
    }
}
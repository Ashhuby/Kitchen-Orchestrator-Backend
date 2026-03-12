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
    private LobbyStateDto? _latestLobbyState;
    private string? _errorToShow;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _playerListContainer = GetNode<VBoxContainer>("VBoxContainer/PlayerListContainer");
        _mapOptionButton = GetNode<OptionButton>("VBoxContainer/MapOptionButton");
        _readyButton = GetNode<Button>("VBoxContainer/ReadyButton");
        _leaveButton = GetNode<Button>("VBoxContainer/LeaveButton");

        _readyButton.Disabled = true;
        _mapOptionButton.Disabled = true;
        _statusLabel.Text = "Connected to Server";

        _mapOptionButton.Clear();
        _mapOptionButton.AddItem("— Select Map —", 0);
        _mapOptionButton.AddItem("TEST (5s)", 1);
        _mapOptionButton.AddItem("Salad Bar", 2);
        _mapOptionButton.AddItem("Sushi Bar", 3);
        _mapOptionButton.AddItem("Burger Diner", 4);

        _readyButton.Pressed += OnReadyPressed;
        _leaveButton.Pressed += OnLeavePressed;
        _mapOptionButton.ItemSelected += OnMapChanged;

        Bootstrap.Connection.OnLobbyStateUpdated += OnLobbyStateUpdated;
        Bootstrap.Connection.OnMatchStarted += OnMatchStarted;
        Bootstrap.Connection.OnError += OnServerError;

        if (Bootstrap.State.CurrentSessionId.HasValue)
            _readyButton.Disabled = false;
    }

    private async void OnReadyPressed()
    {
        if (!Bootstrap.State.CurrentSessionId.HasValue)
        {
            GD.PrintErr("OnReadyPressed: CurrentSessionId is null!");
            return;
        }

        try
        {
            GD.Print($"Sending ready for session {Bootstrap.State.CurrentSessionId.Value}");
            await Bootstrap.Connection.SendReadyAsync(Bootstrap.State.CurrentSessionId.Value);
            _readyButton.Disabled = true;
            _statusLabel.Text = "Ready! Waiting for others...";
        }
        catch (Exception ex)
        {
            GD.PrintErr($"OnReadyPressed FAILED: {ex.GetType().Name}: {ex.Message}");
            _statusLabel.Text = "Error: " + ex.Message;
        }
    }

    private async void OnLeavePressed()
    {
        try
        {
            CleanupSubscriptions();
            await Bootstrap.Connection.DisconnectAsync();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"OnLeavePressed error: {ex.Message}");
        }
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenuScene.tscn");
    }

    private async void OnMapChanged(long index)
    {
        if (index == 0) return;
        if (!_isHost || !Bootstrap.State.CurrentSessionId.HasValue) return;

        string levelId = index switch
        {
            1 => "Map0",
            2 => "Map1",
            3 => "Map2",
            4 => "Map3",
            _ => "Map0"
        };

        try
        {
            await Bootstrap.Connection.ChangeMapAsync(Bootstrap.State.CurrentSessionId.Value, levelId);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"OnMapChanged FAILED: {ex.Message}");
            _statusLabel.Text = "Error: " + ex.Message;
        }
    }

    private void OnServerError(string error)
    {
        _errorToShow = error;
        CallDeferred(nameof(ShowError));
    }

    private void ShowError()
    {
        if (_errorToShow == null) return;
        _statusLabel.Text = $"Error: {_errorToShow}";
        _readyButton.Disabled = false;
        GD.PrintErr($"Server error: {_errorToShow}");
        _errorToShow = null;
    }

    private void OnLobbyStateUpdated(LobbyStateDto lobbyState)
    {
        _latestLobbyState = lobbyState;

        // Keep Bootstrap.State.LevelId current so MatchScene can load the correct map
        Bootstrap.State.LevelId = lobbyState.LevelId;

        CallDeferred(nameof(UpdateUIFromLobbyState));
    }

    private void UpdateUIFromLobbyState()
    {
        if (_latestLobbyState == null) return;
        var lobbyState = _latestLobbyState;

        var me = lobbyState.Players.FirstOrDefault(p => p.PlayerId == Bootstrap.State.PlayerId);
        _isHost = me?.IsHost ?? false;

        _mapOptionButton.Disabled = !_isHost;

        int mapIndex = lobbyState.LevelId switch
        {
            "Map0" => 1,
            "Map1" => 2,
            "Map2" => 3,
            "Map3" => 4,
            _ => 0
        };

        if (_mapOptionButton.Selected != mapIndex)
            _mapOptionButton.Select(mapIndex);

        _readyButton.Disabled = lobbyState.LevelId == null;

        foreach (Node child in _playerListContainer.GetChildren())
            child.QueueFree();

        foreach (var player in lobbyState.Players)
        {
            var label = new Label();
            string hostTag = player.IsHost ? " [HOST]" : "";
            string readyTag = player.IsReady ? " ✓" : "";
            label.Text = $"{player.DisplayName}{hostTag}{readyTag}";
            _playerListContainer.AddChild(label);
        }

        if (_isHost)
            _statusLabel.Text = lobbyState.LevelId == null
                ? "You are the Host — select a map to start"
                : $"You are the Host — {lobbyState.LevelId} selected";
        else
            _statusLabel.Text = lobbyState.LevelId == null
                ? "Waiting for host to select a map..."
                : $"Map: {lobbyState.LevelId} — Ready up!";
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
        Bootstrap.Connection.OnError -= OnServerError;
    }

    public override void _ExitTree()
    {
        CleanupSubscriptions();
    }
}
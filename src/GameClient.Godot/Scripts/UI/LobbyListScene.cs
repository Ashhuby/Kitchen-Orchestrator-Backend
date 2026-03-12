using Godot;
using System;
using System.Collections.Generic;
using KitchenOrchestrator.GameClient.Godot;
using KitchenOrchestrator.Shared.Contracts.DTOs;

/// <summary>
/// Lobby list screen. Sits between MainMenu and LobbyScene.
/// Scene tree expected:
///   LobbyListScene (Control) ← this script
///   └── VBoxContainer
///       ├── TitleLabel       (Label)
///       ├── LobbyContainer   (VBoxContainer) ← lobby rows injected here
///       ├── RefreshButton    (Button)
///       ├── HSeparator       (HSeparator)
///       ├── LobbyNameInput   (LineEdit)  ← name for new lobby
///       ├── CreateButton     (Button)
///       ├── StatusLabel      (Label)
///       └── BackButton       (Button)
/// </summary>
public partial class LobbyListScene : Control
{
    private VBoxContainer _lobbyContainer = null!;
    private Button _refreshButton = null!;
    private LineEdit _lobbyNameInput = null!;
    private Button _createButton = null!;
    private Label _statusLabel = null!;
    private Button _backButton = null!;

    private IReadOnlyList<LobbyInfoDto> _lobbies = new List<LobbyInfoDto>();

    public override void _Ready()
    {
        _lobbyContainer = GetNode<VBoxContainer>("VBoxContainer/LobbyContainer");
        _refreshButton  = GetNode<Button>("VBoxContainer/RefreshButton");
        _lobbyNameInput = GetNode<LineEdit>("VBoxContainer/LobbyNameInput");
        _createButton   = GetNode<Button>("VBoxContainer/CreateButton");
        _statusLabel    = GetNode<Label>("VBoxContainer/StatusLabel");
        _backButton     = GetNode<Button>("VBoxContainer/BackButton");

        _refreshButton.Pressed += OnRefreshPressed;
        _createButton.Pressed  += OnCreatePressed;
        _backButton.Pressed    += OnBackPressed;

        Bootstrap.Connection.OnError += OnServerError;

        _statusLabel.Text = "";
        OnRefreshPressed(); // Auto-refresh on enter
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private async void OnRefreshPressed()
    {
        SetInteractable(false);
        _statusLabel.Text = "Refreshing...";

        try
        {
            _lobbies = await Bootstrap.Connection.GetLobbiesAsync();
            RebuildLobbyList();
            _statusLabel.Text = _lobbies.Count == 0 ? "No open lobbies. Create one!" : "";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Refresh failed: {ex.Message}";
            GD.PrintErr(ex);
        }
        finally
        {
            SetInteractable(true);
        }
    }

    private async void OnCreatePressed()
    {
        string name = _lobbyNameInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"{Bootstrap.State.Profile?.DisplayName ?? "Player"}'s Lobby";

        SetInteractable(false);
        _statusLabel.Text = "Creating lobby...";

        try
        {
            var result = await Bootstrap.Connection.CreateLobbyAsync(name);
            // Server will have sent JoinedLobby event setting State.CurrentSessionId.
            // Give it one frame to process, then transition.
            Bootstrap.State.CurrentSessionId = result.SessionId;
            TransitionToLobby();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Create failed: {ex.Message}";
            GD.PrintErr(ex);
            SetInteractable(true);
        }
    }

    private async void OnJoinPressed(Guid sessionId)
    {
        SetInteractable(false);
        _statusLabel.Text = "Joining lobby...";

        try
        {
            // JoinLobbyAsync now sets State.CurrentSessionId directly from the hub return value
            await Bootstrap.Connection.JoinLobbyAsync(sessionId);

            if (Bootstrap.State.CurrentSessionId.HasValue)
            {
                TransitionToLobby();
            }
            else
            {
                _statusLabel.Text = "Failed to join lobby.";
                SetInteractable(true);
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Join failed: {ex.Message}";
            GD.PrintErr(ex);
            SetInteractable(true);
        }
    }

    private void OnFirstLobbyState(LobbyStateDto _)
    {
        // No longer needed — kept to avoid breaking any lingering subscriptions
        Bootstrap.Connection.OnLobbyStateUpdated -= OnFirstLobbyState;
    }

    private void OnServerError(string message)
    {
        CallDeferred(nameof(ShowError), message);
    }

    private void ShowError(string message)
    {
        _statusLabel.Text = $"Error: {message}";
        SetInteractable(true);
    }

    private void OnBackPressed()
    {
        Cleanup();
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenuScene.tscn");
    }

    // ── UI Helpers ────────────────────────────────────────────────────────────

    private void RebuildLobbyList()
    {
        foreach (Node child in _lobbyContainer.GetChildren())
            child.QueueFree();

        if (_lobbies.Count == 0) return;

        foreach (var lobby in _lobbies)
        {
            // Each lobby entry is a horizontal row
            var row = new HBoxContainer();

            var info = new Label();
            string mapText = lobby.LevelId ?? "No map";
            info.Text = $"{lobby.LobbyName}  [{lobby.PlayerCount}/{lobby.MaxPlayers}]  {mapText}  — Host: {lobby.HostName}";
            info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var joinBtn = new Button();
            joinBtn.Text = "Join";
            // Capture loop variable
            var capturedId = lobby.SessionId;
            joinBtn.Pressed += () => OnJoinPressed(capturedId);

            row.AddChild(info);
            row.AddChild(joinBtn);
            _lobbyContainer.AddChild(row);
        }
    }

    private void SetInteractable(bool enabled)
    {
        _refreshButton.Disabled = !enabled;
        _createButton.Disabled  = !enabled;
        _lobbyNameInput.Editable = enabled;

        foreach (Node child in _lobbyContainer.GetChildren())
        {
            if (child is HBoxContainer row)
                foreach (Node rowChild in row.GetChildren())
                    if (rowChild is Button btn) btn.Disabled = !enabled;
        }
    }

    private void TransitionToLobby()
    {
        Cleanup();
        GetTree().ChangeSceneToFile("res://Scenes/UI/LobbyScene.tscn");
    }

    private void Cleanup()
    {
        Bootstrap.Connection.OnError -= OnServerError;
        Bootstrap.Connection.OnLobbyStateUpdated -= OnFirstLobbyState;
    }

    public override void _ExitTree() => Cleanup();
}
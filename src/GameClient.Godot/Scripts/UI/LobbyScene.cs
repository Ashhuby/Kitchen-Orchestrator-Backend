using Godot;
using System;
using System.Threading.Tasks;
using KitchenOrchestrator.GameClient.Godot;

public partial class LobbyScene : Control
{
    private Label _statusLabel = null!;
    private LineEdit _displayNameInput = null!;
    private Button _loginButton = null!;
    private Button _connectButton = null!;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _displayNameInput = GetNode<LineEdit>("VBoxContainer/DisplayNameInput");
        _loginButton = GetNode<Button>("VBoxContainer/LoginButton");
        _connectButton = GetNode<Button>("VBoxContainer/ConnectButton");

        _connectButton.Disabled = true;
        _statusLabel.Text = "Ready to Login";

        _loginButton.Pressed += OnLoginPressed;
        _connectButton.Pressed += OnConnectPressed;
    }

    private async void OnLoginPressed()
    {
        string displayName = _displayNameInput.Text;
        
        if (string.IsNullOrWhiteSpace(displayName))
        {
            _statusLabel.Text = "Please enter a display name.";
            return;
        }

        _statusLabel.Text = "Logging in (Dev Mode)...";
        _loginButton.Disabled = true;

        bool success = await Bootstrap.Auth.DevLoginAsync(displayName);

        if (success)
        {
            _statusLabel.Text = $"Logged in as {displayName} (DEV)";
            _connectButton.Disabled = false;
        }
        else
        {
            _statusLabel.Text = "Login failed";
            _loginButton.Disabled = false;
        }
    }

    private async void OnConnectPressed()
    {
        _statusLabel.Text = "Connecting to match...";
        _connectButton.Disabled = true;

        bool connected = await Bootstrap.Connection.ConnectAsync();

        if (connected)
        {
            // Subscribe to the match start event
            Bootstrap.Connection.OnMatchStarted += OnMatchStarted;

            try 
            {
                await Bootstrap.Connection.JoinMatchAsync("map1");

                var timeout = DateTime.UtcNow.AddSeconds(5);
                while (!Bootstrap.State.CurrentSessionId.HasValue && DateTime.UtcNow < timeout)
                {
                    await Task.Delay(100);
                }

                if (!Bootstrap.State.CurrentSessionId.HasValue)
                {
                    _statusLabel.Text = "Join Match timed out";
                    _connectButton.Disabled = false;
                    return;
                }

                await Bootstrap.Connection.SendReadyAsync(Bootstrap.State.CurrentSessionId.Value);
                _statusLabel.Text = "Waiting for match...";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Join Match failed";
                GD.PrintErr($"Join Match Error: {ex.Message}");
                _connectButton.Disabled = false;
            }
        }
        else
        {
            _statusLabel.Text = "Connection failed";
            _connectButton.Disabled = false;
        }
    }

    // Handler invoked from GameConnection's SignalR thread
    private void OnMatchStarted(Guid sessionId)
    {
        // Safe cross-thread UI update via Godot's main thread
        CallDeferred(nameof(UpdateStatusForMatchStart));
    }

    private void UpdateStatusForMatchStart()
    {
        _statusLabel.Text = "Match Started!";
    }
}
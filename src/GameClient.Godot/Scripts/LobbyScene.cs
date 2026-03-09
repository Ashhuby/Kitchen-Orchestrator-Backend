using Godot;
using System;
public partial class LobbyScene : Control
{
    private Label _statusLabel = null!;
    private LineEdit _displayNameInput = null!;
    private Button _loginButton = null!;
    private Button _connectButton = null!;

    public override void _Ready()
    {
        // UI Node Initialization
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
        _displayNameInput = GetNode<LineEdit>("VBoxContainer/DisplayNameInput");
        _loginButton = GetNode<Button>("VBoxContainer/LoginButton");
        _connectButton = GetNode<Button>("VBoxContainer/ConnectButton");

        // Initial UI State
        _connectButton.Disabled = true;
        _statusLabel.Text = "Ready to Login";

        // Signal Wiring
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

        _statusLabel.Text = "Logging in...";
        _loginButton.Disabled = true;

        // In a real build, "test_ticket" would be replaced by the 
        // Hex ticket retrieved from Steamworks/GodotSteam.
        bool success = await KitchenOrchestrator.GameClient.Godot.Bootstrap.Auth.LoginAsync("test_ticket", displayName);

        if (success)
        {
            _statusLabel.Text = $"Logged in as {displayName}";
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

        bool connected = await KitchenOrchestrator.GameClient.Godot.Bootstrap.Connection.ConnectAsync();

        if (connected)
        {
            try 
            {
                await KitchenOrchestrator.GameClient.Godot.Bootstrap.Connection.JoinMatchAsync("map1");
                _statusLabel.Text = "Waiting for match...";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Join Match failed";
                Console.WriteLine($"Join Match Error: {ex.Message}");
                _connectButton.Disabled = false;
            }
        }
        else
        {
            _statusLabel.Text = "Connection failed";
            _connectButton.Disabled = false;
        }
    }
}
using Godot;
using System;
using KitchenOrchestrator.GameClient.Godot; // Ensure we have access to Bootstrap

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
        
        // Keeping defensive check as requested
        if (string.IsNullOrWhiteSpace(displayName))
        {
            _statusLabel.Text = "Please enter a display name.";
            return;
        }

        _statusLabel.Text = "Logging in (Dev Mode)...";
        _loginButton.Disabled = true;

        // Swapped LoginAsync for DevLoginAsync to bypass Steam ticket requirement
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
            try 
            {
                await Bootstrap.Connection.JoinMatchAsync("map1");
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
using Godot;
using System;
using KitchenOrchestrator.GameClient.Godot;

public partial class MainMenuScene : Control
{
    private Button _onlineButton   = null!;
    private Button _localButton    = null!;
    private Button _settingsButton = null!;
    private Button _exitButton     = null!;
    private Label  _statusLabel    = null!;

    public override void _Ready()
    {
        _onlineButton   = GetNode<Button>("VBoxContainer/OnlineButton");
        _localButton    = GetNode<Button>("VBoxContainer/LocalButton");
        _settingsButton = GetNode<Button>("VBoxContainer/SettingsButton");
        _exitButton     = GetNode<Button>("VBoxContainer/ExitButton");
        _statusLabel    = GetNode<Label>("VBoxContainer/StatusLabel");

        _statusLabel.Text = "";

        _onlineButton.Pressed   += OnOnlinePressed;
        _localButton.Pressed    += OnLocalPressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _exitButton.Pressed     += OnExitPressed;
    }

    private async void OnOnlinePressed()
    {
        _onlineButton.Disabled = true;
        _statusLabel.Text = "Connecting...";

        try
        {
            bool connected = await Bootstrap.Connection.ConnectAsync();
            if (!connected)
            {
                _statusLabel.Text = "Could not connect to server.";
                _onlineButton.Disabled = false;
                return;
            }

            // Connected — go to lobby list. Session creation happens there.
            GetTree().ChangeSceneToFile("res://Scenes/UI/LobbyListScene.tscn");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Connection error: {ex.Message}");
            _statusLabel.Text = "Connection error.";
            _onlineButton.Disabled = false;
        }
    }

    private void OnLocalPressed()    => GD.Print("Local — not implemented.");
    private void OnSettingsPressed() => GD.Print("Settings — not implemented.");
    private void OnExitPressed()     => GetTree().Quit();
}
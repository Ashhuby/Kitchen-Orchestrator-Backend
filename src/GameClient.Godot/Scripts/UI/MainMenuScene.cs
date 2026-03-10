using Godot;
using System;
using System.Threading.Tasks;
using KitchenOrchestrator.GameClient.Godot;

public partial class MainMenuScene : Control
{
    private Button _onlineButton = null!;
    private Button _localButton = null!;
    private Button _settingsButton = null!;
    private Button _exitButton = null!;

    public override void _Ready()
    {
        _onlineButton = GetNode<Button>("VBoxContainer/OnlineButton");
        _localButton = GetNode<Button>("VBoxContainer/LocalButton");
        _settingsButton = GetNode<Button>("VBoxContainer/SettingsButton");
        _exitButton = GetNode<Button>("VBoxContainer/ExitButton");

        _onlineButton.Pressed += OnOnlinePressed;
        _localButton.Pressed += OnLocalPressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _exitButton.Pressed += OnExitPressed;
    }

    private async void OnOnlinePressed()
    {
        _onlineButton.Disabled = true;
        GD.Print("Connecting to server...");

        try
        {
            // Establish SignalR Connection
            bool connected = await Bootstrap.Connection.ConnectAsync();
            if (!connected)
            {
                GD.PrintErr("Failed to connect to game server.");
                _onlineButton.Disabled = false;
                return;
            }

            // Request to join a match
            await Bootstrap.Connection.JoinMatchAsync("map0");

            // Wait for the server to assign a SessionId (populated via JoinedMatch event)
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!Bootstrap.State.CurrentSessionId.HasValue && DateTime.UtcNow < timeout)
            {
                await Task.Delay(100);
            }

            if (Bootstrap.State.CurrentSessionId.HasValue)
            {
                GD.Print($"Successfully joined session: {Bootstrap.State.CurrentSessionId}");
                GetTree().ChangeSceneToFile("res://Scenes/UI/LobbyScene.tscn");
            }
            else
            {
                GD.PrintErr("Join match timed out.");
                _onlineButton.Disabled = false;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Connection error: {ex.Message}");
            _onlineButton.Disabled = false;
        }
    }

    private void OnLocalPressed()
    {
        GD.Print("LocalButton pressed - Not implemented.");
    }

    private void OnSettingsPressed()
    {
        GD.Print("SettingsButton pressed - Not implemented.");
    }

    private void OnExitPressed()
    {
        GD.Print("ExitButton pressed - Quitting application.");
        GetTree().Quit();
    }
}
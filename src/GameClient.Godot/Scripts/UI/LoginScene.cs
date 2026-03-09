using Godot;
using KitchenOrchestrator.GameClient.Godot;

public partial class LoginScene : Control
{
    private LineEdit _displayNameInput = null!;
    private Button _loginButton = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        _displayNameInput = GetNode<LineEdit>("VBoxContainer/DisplayNameInput");
        _loginButton = GetNode<Button>("VBoxContainer/LoginButton");
        _statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");

        _statusLabel.Text = "Enter a name to start (Dev Mode)";
       
        _loginButton.Pressed += OnLoginPressed;
    }

    private async void OnLoginPressed()
    {
        string displayName = _displayNameInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            _statusLabel.Text = "Please enter a display name.";
            return;
        }

        // Disable UI during async operation
        _loginButton.Disabled = true;
        _displayNameInput.Editable = false;
        _statusLabel.Text = "Logging in...";

        bool success = await Bootstrap.Auth.DevLoginAsync(displayName);

        if (success)
        {
            _statusLabel.Text = "Login successful!";           
            GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenuScene.tscn");
        }
        else
        {
            // Re-enable UI on failure
            _loginButton.Disabled = false;
            _displayNameInput.Editable = true;
            _statusLabel.Text = "Login failed. Please try again.";
        }
    }
}
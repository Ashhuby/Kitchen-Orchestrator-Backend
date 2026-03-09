using Godot;

public partial class MatchScene : Control
{
	private Label _statusLabel = null!;
	private Label _timerLabel = null!;

	public override void _Ready()
	{
		_statusLabel = GetNode<Label>("VBoxContainer/StatusLabel");
		_timerLabel = GetNode<Label>("VBoxContainer/TimerLabel");

		// Initial UI State
		_statusLabel.Text = "Match in progress...";
		_timerLabel.Text = "";
	}
}

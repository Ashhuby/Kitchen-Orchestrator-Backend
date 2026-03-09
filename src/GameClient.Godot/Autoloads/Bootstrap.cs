using Godot;
using KitchenOrchestrator.GameClient.Auth;
using KitchenOrchestrator.GameClient.Configuration;
using KitchenOrchestrator.GameClient.Connection;
using KitchenOrchestrator.GameClient.Models;

namespace KitchenOrchestrator.GameClient.Godot
{
    public partial class Bootstrap : Node
    {
        // Global accessors for game services
        public static AuthManager Auth { get; private set; } = null!;
        public static GameConnection Connection { get; private set; } = null!;
        public static ClientState State { get; private set; } = null!;

        public override void _Ready()
        {
            // Initial configuration these point to Nginx localhost for local development       
            var options = new GameClientOptions
            {
                IdentityApiBaseUrl = "http://localhost",
                GameServerHubUrl = "http://localhost/gamehub",
                SteamAppId = 480 // Spacewars 
            };

            // Initialize the shared state
            State = new ClientState();

            // Initialize Managers  create a single HttpClient here to be reused for the life of the app
			var httpClient = new System.Net.Http.HttpClient();

            Auth = new AuthManager(httpClient, options, State);
            Connection = new GameConnection(options, State);

            GD.Print("Kitchen Orchestrator Client Services Initialized.");
        }
    }
}
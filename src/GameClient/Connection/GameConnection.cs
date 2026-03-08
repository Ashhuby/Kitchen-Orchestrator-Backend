using KitchenOrchestrator.GameClient.Configuration;
using KitchenOrchestrator.GameClient.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace KitchenOrchestrator.GameClient.Connection
{
    public class GameConnection
    {
        private readonly GameClientOptions _options;
        private readonly ClientState _state;
        private HubConnection? _connection;

        public GameConnection(GameClientOptions options, ClientState state)
        {
            _options = options;
            _state = state;
        }

        public async Task<bool> ConnectAsync()
        {
            // Verify we have a valid session before even trying to connect
            if (!_state.IsTokenValid)
            {
                Console.WriteLine("ConnectAsync aborted: JWT is missing or expired.");
                return false;
            }

            try
            {
                // SignalR uses the query string for the token because WebSockets                
                _connection = new HubConnectionBuilder()
                    .WithUrl($"{_options.GameServerHubUrl}?access_token={_state.Jwt}")
                    .WithAutomaticReconnect()
                    .Build();

                // Register Event Handlers
                _connection.On<Guid>("JoinedMatch", (sessionId) =>
                {
                    _state.CurrentSessionId = sessionId;
                    _state.IsConnectedToMatch = true;
                    Console.WriteLine($"Joined match session: {sessionId}");
                });

                _connection.On<Guid>("MatchStarted", (sessionId) =>
                {
                    Console.WriteLine($"Match started: {sessionId}");
                });

                // Open the WebSocket line hooray
                await _connection.StartAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR Connection Error: {ex.Message}");
                _state.IsConnectedToMatch = false;
                return false;
            }
        }

        public async Task JoinMatchAsync(string levelId)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Cannot join match: Not connected to server.");

            await _connection.InvokeAsync("JoinMatch", levelId);
        }

        public async Task SendReadyAsync(Guid sessionId)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Cannot send ready status: Not connected to server.");

            await _connection.InvokeAsync("PlayerReady", sessionId);
        }

        public async Task DisconnectAsync()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
                _connection = null;
            }

            _state.IsConnectedToMatch = false;
            _state.CurrentSessionId = null;
        }
    }
}
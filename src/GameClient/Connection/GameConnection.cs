using KitchenOrchestrator.GameClient.Configuration;
using KitchenOrchestrator.GameClient.Models;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.GameLogic.Recipes;
using Microsoft.AspNetCore.SignalR.Client;

namespace KitchenOrchestrator.GameClient.Connection
{
    public class GameConnection
    {
        private readonly GameClientOptions _options;
        private readonly ClientState _state;
        private HubConnection? _connection;

        // C# events to allow UI components to subscribe
        public event Action<Guid>? OnMatchStarted;
        public event Action<LobbyStateDto>? OnLobbyStateUpdated;
        public event Action<MatchStateDto>? OnMatchStateUpdated;
        public event Action<DeliveryResult>? OnDeliveryResult;

        public GameConnection(GameClientOptions options, ClientState state)
        {
            _options = options;
            _state = state;
        }

        public async Task<bool> ConnectAsync()
        {
            if (!_state.IsTokenValid)
            {
                Console.WriteLine("ConnectAsync aborted: JWT is missing or expired.");
                return false;
            }

            try
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl($"{_options.GameServerHubUrl}?access_token={_state.Jwt}")
                    .Build();

                _connection.On<Guid>("JoinedMatch", (sessionId) =>
                {
                    _state.CurrentSessionId = sessionId;
                    _state.IsConnectedToMatch = true;
                    Console.WriteLine($"Joined match session: {sessionId}");
                });

                _connection.On<Guid>("MatchStarted", (sessionId) =>
                {
                    Console.WriteLine($"Match started: {sessionId}");
                    OnMatchStarted?.Invoke(sessionId);
                });

                _connection.On<LobbyStateDto>("LobbyStateUpdated", (lobbyState) =>
                {
                    OnLobbyStateUpdated?.Invoke(lobbyState);
                });

                _connection.On<MatchStateDto>("MatchStateUpdated", (matchState) =>
                {
                    OnMatchStateUpdated?.Invoke(matchState);
                });

                _connection.On<DeliveryResult>("DeliveryResult", (result) =>
                {
                    OnDeliveryResult?.Invoke(result);
                });

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

        public async Task ChangeMapAsync(Guid sessionId, string levelId)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to server.");

            await _connection.InvokeAsync("ChangeMap", sessionId, levelId);
        }

        public async Task ChangeMapAsync(Guid sessionId, string levelId)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to server.");

            await _connection.InvokeAsync("ChangeMap", sessionId, levelId);
        }

        public async Task SendReadyAsync(Guid sessionId)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Cannot send ready status: Not connected to server.");

            await _connection.InvokeAsync("PlayerReady", sessionId);
        }

        public async Task DeliverDishAsync(Guid sessionId, List<Ingredient> ingredients)
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Cannot deliver dish: Not connected to server.");

            await _connection.InvokeAsync("DeliverDish", sessionId, ingredients);
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
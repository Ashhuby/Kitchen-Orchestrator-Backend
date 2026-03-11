using KitchenOrchestrator.GameClient.Configuration;
using KitchenOrchestrator.GameClient.Models;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.SignalR.Client;

namespace KitchenOrchestrator.GameClient.Connection
{
    public class GameConnection
    {
        private readonly GameClientOptions _options;
        private readonly ClientState _state;
        private HubConnection? _connection;

        // Lobby list screen events
        public event Action<IReadOnlyList<LobbyInfoDto>>? OnLobbiesReceived;

        // Lobby screen events
        public event Action<LobbyStateDto>? OnLobbyStateUpdated;
        public event Action<Guid>? OnMatchStarted;

        // Match screen events
        public event Action<MatchStateDto>? OnMatchStateUpdated;
        public event Action<DeliveryResult>? OnDeliveryResult;

        // Error event — hub sends a plain string for now
        public event Action<string>? OnError;

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

                // Lobby list
                _connection.On<Guid>("JoinedLobby", (sessionId) =>
                {
                    // Kept for backwards compat but session ID now comes from InvokeAsync return value
                    _state.CurrentSessionId = sessionId;
                    _state.IsConnectedToMatch = true;
                });

                // Lobby state
                _connection.On<LobbyStateDto>("LobbyStateUpdated", dto =>
                    OnLobbyStateUpdated?.Invoke(dto));

                // Match starting
                _connection.On<Guid>("MatchStarted", sessionId =>
                    OnMatchStarted?.Invoke(sessionId));

                // In-match
                _connection.On<MatchStateDto>("MatchStateUpdated", dto =>
                    OnMatchStateUpdated?.Invoke(dto));

                _connection.On<DeliveryResult>("DeliveryResult", result =>
                    OnDeliveryResult?.Invoke(result));

                // Errors
                _connection.On<string>("Error", msg =>
                    OnError?.Invoke(msg));

                await _connection.StartAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR connection error: {ex.Message}");
                return false;
            }
        }

        // ── Lobby List ────────────────────────────────────────────────────────

        public async Task<IReadOnlyList<LobbyInfoDto>> GetLobbiesAsync()
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<IReadOnlyList<LobbyInfoDto>>("GetLobbies");
        }

        public async Task<LobbyCreatedDto> CreateLobbyAsync(string lobbyName)
        {
            EnsureConnected();
            return await _connection!.InvokeAsync<LobbyCreatedDto>("CreateLobby", lobbyName);
        }

        public async Task JoinLobbyAsync(Guid sessionId)
        {
            EnsureConnected();
            var returnedSessionId = await _connection!.InvokeAsync<Guid?>("JoinLobby", sessionId);
            if (returnedSessionId.HasValue)
            {
                _state.CurrentSessionId = returnedSessionId.Value;
                _state.IsConnectedToMatch = true;
            }
        }

        // ── In-Lobby ──────────────────────────────────────────────────────────

        public async Task ChangeMapAsync(Guid sessionId, string levelId)
        {
            EnsureConnected();
            await _connection!.InvokeAsync("ChangeMap", sessionId, levelId);
        }

        public async Task SendReadyAsync(Guid sessionId)
        {
            EnsureConnected();
            await _connection!.InvokeAsync("PlayerReady", sessionId);
        }

        // ── In-Match ──────────────────────────────────────────────────────────

        public void SendPositionAsync(PositionUpdateDto dto)
        {
            if (_connection?.State != HubConnectionState.Connected) return;
            // Fire and forget — dropped frames are acceptable for movement
            _ = _connection.InvokeAsync("UpdatePosition", dto);
        }

        public void SendActionAsync(StationActionRequest request)
        {
            if (_connection?.State != HubConnectionState.Connected) return;
            _ = _connection.InvokeAsync("RequestAction", request);
        }

        // ── Disconnect ────────────────────────────────────────────────────────

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

        private void EnsureConnected()
        {
            if (_connection == null || _connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException("Not connected to server.");
        }
    }
}
using KitchenOrchestrator.GameServer.Hubs;
using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Orders;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.GameServer.Services
{
    public class GameLoopService : BackgroundService
    {
        private readonly IMatchSessionService _sessionService;
        private readonly IMatchResultSubmissionService _submissionService;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<GameLoopService> _logger;

        private const int TickDelayMs = 100;
        private const float DeltaTime = 0.1f;

        public GameLoopService(
            IMatchSessionService sessionService,
            IMatchResultSubmissionService submissionService,
            IHubContext<GameHub> hubContext,
            ILogger<GameLoopService> logger)
        {
            _sessionService = sessionService;
            _submissionService = submissionService;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GameLoopService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TickAllSessionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during game loop tick.");
                }

                await Task.Delay(TickDelayMs, stoppingToken);
            }
        }

        private async Task TickAllSessionsAsync()
        {
            var sessions = _sessionService.GetActiveSessions();
            foreach (var session in sessions)
                await TickSessionAsync(session);
        }

        private async Task TickSessionAsync(MatchSession session)
        {
            session.TimeRemainingSeconds -= DeltaTime;

            if (session.TimeRemainingSeconds <= 0f)
            {
                await EndMatchAsync(session);
                return;
            }

            TickOrders(session);
            TickStations(session);
            TrySpawnOrder(session);

            await BroadcastMatchStateAsync(session);
        }

        // ── Orders ────────────────────────────────────────────────────────────

        private void TickOrders(MatchSession session)
        {
            lock (session.Orders)
            {
                foreach (var order in session.Orders.Where(o => o.Status == OrderStatus.InProgress))
                {
                    order.Timer.Tick(DeltaTime);
                    if (order.Timer.IsExpired)
                    {
                        order.Status = OrderStatus.TimedOut;
                        session.FailedOrders++;
                        _logger.LogInformation("Order {OrderId} timed out in session {SessionId}.",
                            order.OrderId, session.SessionId);
                    }
                }
            }
        }

        private void TrySpawnOrder(MatchSession session)
        {
            var levelDef = session.LevelDefinition;
            if (levelDef == null) return;

            session.TimeSinceLastOrderSpawn += DeltaTime;
            if (session.TimeSinceLastOrderSpawn < levelDef.OrderSpawnIntervalSeconds) return;

            int activeCount;
            lock (session.Orders)
            {
                activeCount = session.Orders.Count(o => o.Status == OrderStatus.InProgress);
            }

            if (activeCount >= levelDef.MaxSimultaneousOrders) return;

            float progress = 1f - (session.TimeRemainingSeconds / levelDef.DurationSeconds);
            var recipe = OrderGenerator.Generate(progress);
            var newOrder = new ActiveOrder(recipe, 60f);
            newOrder.Status = OrderStatus.InProgress;

            lock (session.Orders)
            {
                session.Orders.Add(newOrder);
            }

            session.TimeSinceLastOrderSpawn = 0f;
            _logger.LogInformation("Spawned order {Recipe} in session {SessionId}.", recipe.Name, session.SessionId);
        }

        // ── Stations ──────────────────────────────────────────────────────────

        private void TickStations(MatchSession session)
        {
            foreach (var station in session.Stations.Values)
            {
                if (station.Type == StationType.Counter ||
                    station.Type == StationType.IngredientSource ||
                    station.Type == StationType.DeliveryCounter)
                    continue;

                station.Tick(DeltaTime);
            }
        }

        // ── Match End ─────────────────────────────────────────────────────────

        private async Task EndMatchAsync(MatchSession session)
        {
            session.State = MatchState.Completed;
            _logger.LogInformation("Match {SessionId} completed.", session.SessionId);

            // Send the final state with State = "Completed" so clients transition out
            await BroadcastMatchStateAsync(session);

            _ = Task.Run(async () =>
            {
                try { await _submissionService.SubmitAsync(session); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to submit results for session {SessionId}.", session.SessionId);
                }
            });
        }

        // ── Broadcast ─────────────────────────────────────────────────────────

        private async Task BroadcastMatchStateAsync(MatchSession session)
        {
            var players = session.Players
                .Select(p => new PlayerPositionDto(p.PlayerId, p.DisplayName, p.X, p.Y))
                .ToList()
                .AsReadOnly();

            var stations = session.Stations.Values
                .Select(s => new StationStateDto(
                    s.StationId,
                    s.Type.ToString(),
                    s.HeldItem?.Ingredient.ToString(),
                    s.HeldItem?.PrepState.ToString(),
                    s.ProgressNormalized,
                    s.OccupyingPlayerId != null))
                .ToList()
                .AsReadOnly();

            var state = new MatchStateDto(
                session.SessionId,
                session.State.ToString(),   // "Active", "Completed", "Abandoned"
                players,
                stations,
                session.TimeRemainingSeconds,
                session.TotalScore);

            await _hubContext.Clients
                .Group(session.SessionId.ToString())
                .SendAsync("MatchStateUpdated", state);
        }
    }
}
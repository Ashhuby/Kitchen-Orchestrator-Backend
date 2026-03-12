using KitchenOrchestrator.GameServer.Hubs;
using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Levels;
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
        private const float BurnGracePeriodSeconds = 5f;

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
            _logger.LogInformation("Game Loop Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TickAllSessionsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Game Loop tick.");
                }

                await Task.Delay(TickDelayMs, stoppingToken);
            }
        }

        private async Task TickAllSessionsAsync()
        {
            var activeSessions = _sessionService.GetActiveSessions();

            foreach (var session in activeSessions)
            {
                await TickSessionAsync(session);
            }
        }

        private async Task TickSessionAsync(MatchSession session)
        {
            session.TimeRemainingSeconds -= DeltaTime;

            if (session.TimeRemainingSeconds <= 0)
            {
                EndMatch(session);
                return;
            }

            // --- Order timers ---
            lock (session.Orders)
            {
                foreach (var order in session.Orders.Where(o => o.Status == OrderStatus.InProgress))
                {
                    order.Timer.Tick(DeltaTime);

                    if (order.Timer.IsExpired)
                    {
                        order.Status = OrderStatus.TimedOut;
                        session.FailedOrders++;
                    }
                }
            }

            // --- Order spawning ---
            var levelDef = session.LevelDefinition;
            if (levelDef != null)
            {
                session.TimeSinceLastOrderSpawn += DeltaTime;

                if (session.TimeSinceLastOrderSpawn >= levelDef.OrderSpawnIntervalSeconds &&
                    session.Orders.Count(o => o.Status == OrderStatus.InProgress) < levelDef.MaxSimultaneousOrders)
                {
                    float difficultyProgress = 1f - (session.TimeRemainingSeconds / levelDef.DurationSeconds);
                    var recipe = OrderGenerator.Generate(difficultyProgress);
                    var newOrder = new ActiveOrder(recipe, 60f);

                    lock (session.Orders)
                    {
                        session.Orders.Add(newOrder);
                    }

                    session.TimeSinceLastOrderSpawn = 0f;
                }
            }

            // --- Station ticking ---
            TickStations(session);

            await BroadcastMatchStateAsync(session);
        }

        private void TickStations(MatchSession session)
        {
            foreach (var station in session.Stations.Values)
            {
                if (station.HeldItem == null) continue;

                switch (station.Type)
                {
                    case StationType.ChoppingBoard:
                        if (!station.OccupyingPlayerId.HasValue) continue;
                        if (!station.IsComplete)
                        {
                            station.ProgressSeconds += DeltaTime;
                            if (station.IsComplete)
                            {
                                station.HeldItem.PrepState = ItemPrepState.Chopped;
                                station.OccupyingPlayerId = null;
                                _logger.LogDebug("Station {Id}: chopping complete.", station.StationId);
                            }
                        }
                        break;

                    case StationType.Stove:
                        if (station.DurationSeconds <= 0) continue;
                        if (!station.IsComplete)
                        {
                            station.ProgressSeconds += DeltaTime;
                            if (station.IsComplete)
                            {
                                station.HeldItem.PrepState = ItemPrepState.Cooked;
                                _logger.LogDebug("Station {Id}: cooking complete. Burn timer started.", station.StationId);
                            }
                        }
                        else if (station.HeldItem.PrepState == ItemPrepState.Cooked)
                        {
                            station.ProgressSeconds += DeltaTime;
                            float burnThreshold = station.DurationSeconds + BurnGracePeriodSeconds;
                            if (station.ProgressSeconds >= burnThreshold)
                            {
                                station.HeldItem.PrepState = ItemPrepState.Burned;
                                _logger.LogInformation("Station {Id}: item burned!", station.StationId);
                            }
                        }
                        break;
                }
            }
        }

        private async Task BroadcastMatchStateAsync(MatchSession session)
        {
            List<PlayerPositionDto> playerPositions;
            lock (session.Players)
            {
                playerPositions = session.Players
                    .Select(p => new PlayerPositionDto(p.PlayerId, p.DisplayName, p.X, p.Y))
                    .ToList();
            }

            var stationStates = session.Stations.Values
                .Select(s => new StationStateDto(
                    s.StationId,
                    s.Type.ToString(),
                    s.HeldItem?.Ingredient.ToString(),
                    s.HeldItem?.PrepState.ToString(),
                    s.ProgressNormalized,
                    s.OccupyingPlayerId.HasValue
                ))
                .ToList();

            var matchState = new MatchStateDto(
                session.SessionId,
                playerPositions.AsReadOnly(),
                stationStates.AsReadOnly(),
                session.TimeRemainingSeconds,
                session.TotalScore
            );

            await _hubContext.Clients
                .Group(session.SessionId.ToString())
                .SendAsync("MatchStateUpdated", matchState);
        }

        private void EndMatch(MatchSession session)
        {
            session.State = MatchState.Completed;
            _logger.LogInformation("Match {SessionId} ended. Submitting results...", session.SessionId);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _submissionService.SubmitAsync(session);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to submit results for session {SessionId}", session.SessionId);
                }
            });
        }
    }
}
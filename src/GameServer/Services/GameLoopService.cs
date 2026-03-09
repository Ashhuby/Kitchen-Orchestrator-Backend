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

        // Broadcast every 3rd tick (10Hz tick rate -> ~3Hz broadcast)
        private int _tickCount = 0;
        private const int BroadcastEveryNTicks = 3;

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

            const int tickDelayMs = 100;
            const float deltaTime = 0.1f;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _tickCount++;
                    bool shouldBroadcast = (_tickCount % BroadcastEveryNTicks == 0);
                    TickAllSessions(deltaTime, shouldBroadcast);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Game Loop tick.");
                }

                await Task.Delay(tickDelayMs, stoppingToken);
            }
        }

        private void TickAllSessions(float deltaTime, bool shouldBroadcast)
        {
            var activeSessions = _sessionService.GetActiveSessions();

            foreach (var session in activeSessions)
            {
                TickSession(session, deltaTime, shouldBroadcast);
            }
        }

        private void TickSession(MatchSession session, float deltaTime, bool shouldBroadcast)
        {
            session.TimeRemainingSeconds -= deltaTime;

            if (session.TimeRemainingSeconds <= 0)
            {
                EndMatch(session);
                return;
            }

            lock (session.Orders)
            {
                foreach (var order in session.Orders.Where(o => o.Status == OrderStatus.InProgress))
                {
                    order.Timer.Tick(deltaTime);

                    if (order.Timer.IsExpired)
                    {
                        order.Status = OrderStatus.TimedOut;
                        session.FailedOrders++;
                    }
                }
            }

            var levelDef = LevelRegistry.GetById(session.LevelId);
            if (levelDef != null)
            {
                session.TimeSinceLastOrderSpawn += deltaTime;

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

            // Broadcast live state to all clients in this session at reduced rate
            if (shouldBroadcast)
            {
                var state = BuildMatchState(session);
                _ = _hubContext.Clients.Group(session.SessionId.ToString())
                    .SendAsync("MatchStateUpdated", state);
            }
        }

        private void EndMatch(MatchSession session)
        {
            // Transition state immediately to remove from next tick cycle
            session.State = MatchState.Completed;

            // Explicit final broadcast so clients know the match ended.
            // Cannot rely on the tick loop here — GetActiveSessions() filters
            // to Active only, so this session is already invisible to it.
            var finalState = BuildMatchState(session);
            _ = _hubContext.Clients.Group(session.SessionId.ToString())
                .SendAsync("MatchStateUpdated", finalState);

            _logger.LogInformation("Match {SessionId} ended. Submitting results...", session.SessionId);

            // Explicit Fire-and-Forget pattern
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

        private MatchStateDto BuildMatchState(MatchSession session)
        {
            List<ActiveOrderDto> orders;
            lock (session.Orders)
            {
                orders = session.Orders
                    .Where(o => o.Status == OrderStatus.InProgress)
                    .Select(o => new ActiveOrderDto(
                        o.OrderId,
                        o.Recipe.Name,
                        o.Recipe.RequiredIngredients.Select(i => i.ToString()).ToList().AsReadOnly(),
                        o.Timer.TimeRemaining,
                        o.Timer.TotalDuration
                    ))
                    .ToList();
            }

            var players = session.Players.Select(p => new MatchPlayerDto(
                p.PlayerId,
                p.DisplayName,
                p.Score,
                p.OrdersDelivered
            )).ToList().AsReadOnly();

            return new MatchStateDto(
                session.SessionId,
                session.State.ToString(),
                session.TimeRemainingSeconds,
                session.TotalScore,
                orders,
                players
            );
        }
    }
}
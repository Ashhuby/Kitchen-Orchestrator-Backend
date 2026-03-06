using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Levels;
using KitchenOrchestrator.Shared.GameLogic.Orders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.GameServer.Services
{
    public class GameLoopService : BackgroundService
    {
        private readonly IMatchSessionService _sessionService;
        private readonly IMatchResultSubmissionService _submissionService;
        private readonly ILogger<GameLoopService> _logger;

        public GameLoopService(
            IMatchSessionService sessionService,
            IMatchResultSubmissionService submissionService,
            ILogger<GameLoopService> logger)
        {
            _sessionService = sessionService;
            _submissionService = submissionService;
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
                    TickAllSessions(deltaTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Game Loop tick.");
                }

                await Task.Delay(tickDelayMs, stoppingToken);
            }
        }

        private void TickAllSessions(float deltaTime)
        {
            var activeSessions = _sessionService.GetActiveSessions();

            foreach (var session in activeSessions)
            {
                TickSession(session, deltaTime);
            }
        }

        private void TickSession(MatchSession session, float deltaTime)
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
        }

        private void EndMatch(MatchSession session)
        {
            // Transition state immediately to remove from next Tick cycle
            session.State = MatchState.Completed;
            
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
    }
}
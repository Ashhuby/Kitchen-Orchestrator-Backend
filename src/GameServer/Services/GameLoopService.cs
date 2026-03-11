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
<<<<<<< Updated upstream
=======
        private readonly IHubContext<KitchenOrchestrator.GameServer.Hubs.GameHub> _hubContext;
>>>>>>> Stashed changes
        private readonly ILogger<GameLoopService> _logger;

        // Tick rate: 100ms = 10Hz.
        // Position updates from clients arrive at 10Hz — we broadcast once per tick,
        // regardless of how many UpdatePosition calls arrived since the last tick.
        private const int TickDelayMs = 100;
        private const float DeltaTime = 0.1f;

        // How long a stove item can sit at Cooked before becoming Burned.
        private const float BurnGracePeriodSeconds = 5f;

        public GameLoopService(
            IMatchSessionService sessionService,
            IMatchResultSubmissionService submissionService,
<<<<<<< Updated upstream
=======
            IHubContext<KitchenOrchestrator.GameServer.Hubs.GameHub> hubContext,
>>>>>>> Stashed changes
            ILogger<GameLoopService> logger)
        {
            _sessionService = sessionService;
            _submissionService = submissionService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Game Loop Service started.");

<<<<<<< Updated upstream
            const int tickDelayMs = 100;
            const float deltaTime = 0.1f;

=======
>>>>>>> Stashed changes
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
<<<<<<< Updated upstream
                    TickAllSessions(deltaTime);
=======
                    await TickAllSessionsAsync();
>>>>>>> Stashed changes
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Game Loop tick.");
                }

<<<<<<< Updated upstream
                await Task.Delay(tickDelayMs, stoppingToken);
            }
        }

        private void TickAllSessions(float deltaTime)
=======
                await Task.Delay(TickDelayMs, stoppingToken);
            }
        }

        private async Task TickAllSessionsAsync()
>>>>>>> Stashed changes
        {
            var activeSessions = _sessionService.GetActiveSessions();

            foreach (var session in activeSessions)
            {
<<<<<<< Updated upstream
                TickSession(session, deltaTime);
            }
        }

        private void TickSession(MatchSession session, float deltaTime)
=======
                await TickSessionAsync(session);
            }
        }

        private async Task TickSessionAsync(MatchSession session)
>>>>>>> Stashed changes
        {
            // --- Match timer ---
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
            var levelDef = LevelRegistry.GetById(session.LevelId);
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
<<<<<<< Updated upstream
=======

            // --- Station ticking ---
            // Timed stations (ChoppingBoard, Stove) advance their progress each tick.
            // The server owns all station timers — clients only display what they receive.
            bool stationsDirty = TickStations(session);

            // Always broadcast during an active match.
            // The dirty flag optimisation can be added later once correctness is confirmed.
            await BroadcastMatchStateAsync(session);
        }

        /// <summary>
        /// Advances all timed station timers. Returns true if any station state changed
        /// this tick (so the broadcast knows to send even if no player moved).
        /// </summary>
        private bool TickStations(MatchSession session)
        {
            bool anyChange = false;

            foreach (var station in session.Stations.Values)
            {
                if (station.HeldItem == null) continue;

                switch (station.Type)
                {
                    case StationType.ChoppingBoard:
                        // Only tick if a player is actively chopping (OccupyingPlayerId is set)
                        if (!station.OccupyingPlayerId.HasValue) continue;

                        if (!station.IsComplete)
                        {
                            station.ProgressSeconds += DeltaTime;
                            anyChange = true;

                            if (station.IsComplete)
                            {
                                // Chopping done — mark item as chopped, release occupying player lock
                                station.HeldItem.PrepState = ItemPrepState.Chopped;
                                station.OccupyingPlayerId = null;
                                _logger.LogDebug("Station {Id}: chopping complete.", station.StationId);
                            }
                        }
                        break;

                    case StationType.Stove:
                        // Stove ticks autonomously once an item is deposited (BeginProcessing called on deposit).
                        if (station.DurationSeconds <= 0) continue;

                        if (!station.IsComplete)
                        {
                            station.ProgressSeconds += DeltaTime;
                            anyChange = true;

                            if (station.IsComplete)
                            {
                                station.HeldItem.PrepState = ItemPrepState.Cooked;
                                _logger.LogDebug("Station {Id}: cooking complete. Burn timer started.", station.StationId);
                            }
                        }
                        else if (station.HeldItem.PrepState == ItemPrepState.Cooked)
                        {
                            // Item is cooked but hasn't been collected — tick the burn grace period.
                            // We reuse ProgressSeconds beyond DurationSeconds for this.
                            station.ProgressSeconds += DeltaTime;
                            anyChange = true;

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

            return anyChange;
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
>>>>>>> Stashed changes
        }

        private void EndMatch(MatchSession session)
        {
            // Transition state immediately to remove from next Tick cycle
            session.State = MatchState.Completed;
            
            _logger.LogInformation("Match {SessionId} ended. Submitting results...", session.SessionId);

<<<<<<< Updated upstream
            // Explicit Fire-and-Forget pattern
=======
>>>>>>> Stashed changes
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
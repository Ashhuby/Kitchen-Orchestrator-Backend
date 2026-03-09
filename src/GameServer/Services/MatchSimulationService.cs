using KitchenOrchestrator.GameServer.Models;
using KitchenOrchestrator.Shared.Contracts.DTOs;
using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;
using KitchenOrchestrator.Shared.GameLogic.Scoring;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.GameServer.Services
{
    public interface IMatchSimulationService
    {
        DeliveryResult DeliverDish(Guid sessionId, Guid playerId, List<Ingredient> ingredients);
    }

    public class MatchSimulationService : IMatchSimulationService
    {
        private readonly IMatchSessionService _sessionService;
        private readonly ILogger<MatchSimulationService> _logger;

        public MatchSimulationService(
            IMatchSessionService sessionService,
            ILogger<MatchSimulationService> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        public DeliveryResult DeliverDish(Guid sessionId, Guid playerId, List<Ingredient> ingredients)
        {
            var session = _sessionService.GetSession(sessionId);

            if (session == null)
                return new DeliveryResult(false, 0, false, "Session not found.");

            if (session.State != MatchState.Active)
                return new DeliveryResult(false, 0, false, "Match is not active.");

            lock (session.Orders)
            {
                // Find the first InProgress order whose recipe matches the submitted ingredients.
                // e.g. [Patty, BurgerBun] matches [BurgerBun, Patty] -- a player should not be penalised for a different assembly order.
                var matchedOrder = session.Orders.FirstOrDefault(o =>
                    o.Status == OrderStatus.InProgress &&
                    new HashSet<Ingredient>(ingredients).SetEquals(o.Recipe.RequiredIngredients));

                if (matchedOrder == null)
                    return new DeliveryResult(false, 0, false, "No matching active order found.");

                // Mark delivered before touching score so the order cannot be double-claimed
                matchedOrder.Status = OrderStatus.Delivered;

                // Score: seconds remaining is floored to int for ScoreCalculator
                int secondsRemaining = (int)matchedOrder.Timer.TimeRemaining;

                // Perfect = delivered with more than 80% time remaining on the order timer
                bool isPerfect = matchedOrder.Timer.TimeRemaining / matchedOrder.Timer.TotalDuration > 0.8f;

                int score = ScoreCalculator.Calculate(matchedOrder.Recipe, secondsRemaining, isPerfect);

                // Update session-level totals
                session.TotalScore += score;
                session.CompletedOrders++;
                if (isPerfect) session.PerfectOrders++;

                // Update the delivering player's individual stats
                var player = session.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (player != null)
                {
                    player.Score += score;
                    player.OrdersDelivered++;
                }

                _logger.LogInformation(
                    "Player {PlayerId} delivered {Recipe} in session {SessionId}. Score: +{Score} Perfect: {IsPerfect}",
                    playerId, matchedOrder.Recipe.Name, sessionId, score, isPerfect);

                return new DeliveryResult(true, score, isPerfect, null);
            }
        }
    }
}
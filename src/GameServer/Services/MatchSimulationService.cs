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
                var matchingOrder = session.Orders.FirstOrDefault(o =>
                    o.Status == OrderStatus.InProgress &&
                    o.Recipe.RequiredIngredients.ToHashSet().SetEquals(ingredients));

                if (matchingOrder == null)
                    return new DeliveryResult(false, 0, false, "No matching order found.");

                matchingOrder.Status = OrderStatus.Delivered;

                int secondsRemaining = (int)matchingOrder.Timer.TimeRemaining;
                bool isPerfect = matchingOrder.Timer.TimeRemaining / matchingOrder.Timer.TotalDuration > 0.8f;
                int score = ScoreCalculator.Calculate(matchingOrder.Recipe, secondsRemaining, isPerfect);

                var player = session.Players.FirstOrDefault(p => p.PlayerId == playerId);
                if (player != null)
                {
                    player.Score += score;
                    player.OrdersDelivered++;
                }

                session.TotalScore += score;
                session.CompletedOrders++;
                if (isPerfect) session.PerfectOrders++;

                _logger.LogInformation(
                    "Player {PlayerId} delivered {Recipe} for {Score} points. Perfect: {IsPerfect}",
                    playerId, matchingOrder.Recipe.Name, score, isPerfect);

                return new DeliveryResult(true, score, isPerfect, null);
            }
        }
    }
}
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
        DeliveryResult TryDeliver(MatchSession session, List<Ingredient> ingredients);
    }

    public class MatchSimulationService : IMatchSimulationService
    {
        private readonly ILogger<MatchSimulationService> _logger;

        public MatchSimulationService(ILogger<MatchSimulationService> logger)
        {
            _logger = logger;
        }

        public DeliveryResult TryDeliver(MatchSession session, List<Ingredient> ingredients)
        {
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

                _logger.LogInformation(
                    "Delivered {Recipe} for {Score} points. Perfect: {IsPerfect}",
                    matchingOrder.Recipe.Name, score, isPerfect);

                return new DeliveryResult(true, score, isPerfect, null);
            }
        }
    }
}
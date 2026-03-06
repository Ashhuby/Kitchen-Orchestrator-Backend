using KitchenOrchestrator.Shared.GameLogic.Recipes;
namespace KitchenOrchestrator.Shared.GameLogic.Scoring
{
    public static class ScoreCalculator
    {
        public static int Calculate(RecipeDefinition recipe, int secondsRemaining, bool isPerfect)
        {
            int score = recipe.BaseScore; 
            if(secondsRemaining > 0) score += (secondsRemaining * 10);
            if(isPerfect) score *=2; 
            return score;
        }
    } 
}
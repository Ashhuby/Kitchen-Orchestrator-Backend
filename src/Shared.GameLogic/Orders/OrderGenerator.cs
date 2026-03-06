using KitchenOrchestrator.Shared.GameLogic.Recipes;
namespace KitchenOrchestrator.Shared.GameLogic.Orders
{
    public static class OrderGenerator
    {
        public static RecipeDefinition Generate(float difficulty)
        {
            var generatedRecipe = difficulty < 0.5f
            ? RecipeRegistry.AllRecipes.Where(r => r.BaseScore <= 300).ToList()
            : RecipeRegistry.AllRecipes.ToList();

            int index = Random.Shared.Next(0, generatedRecipe.Count);
            return generatedRecipe[index];
        }
    }
}
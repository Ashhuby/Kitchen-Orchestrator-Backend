namespace KitchenOrchestrator.Shared.GameLogic.Recipes
{
    public class RecipeDefinition
    {
        public string Name { get; }
        public IReadOnlyList<Ingredient> RequiredIngredients { get; }
        public int BaseScore { get; }

        public RecipeDefinition(string name, IReadOnlyList<Ingredient> requiredIngredients, int baseScore)
        {
            Name = name;
            RequiredIngredients = requiredIngredients;
            BaseScore = baseScore;
        }

    }
}
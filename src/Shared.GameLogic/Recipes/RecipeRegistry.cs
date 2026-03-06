namespace KitchenOrchestrator.Shared.GameLogic.Recipes
{
    public static class RecipeRegistry
    {
        public static IReadOnlyList<RecipeDefinition> AllRecipes { get; } = new List<RecipeDefinition>
        {
            new RecipeDefinition("PlainBurger", new List<Ingredient> { Ingredient.BurgerBun, Ingredient.Patty }, 200),
            new RecipeDefinition("CheeseBurger", new List<Ingredient> { Ingredient.BurgerBun, Ingredient.Patty, Ingredient.Cheese }, 400),
            new RecipeDefinition("Salad",  new List<Ingredient> { Ingredient.Lettuce, Ingredient.Tomato }, 200),
            new RecipeDefinition("Sushi",  new List<Ingredient> { Ingredient.Salmon, Ingredient.Rice }, 300)
        };

        public static RecipeDefinition? GetByName(string name)
        {
            return AllRecipes.FirstOrDefault(r => 
                r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
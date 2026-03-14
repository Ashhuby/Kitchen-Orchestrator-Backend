using KitchenOrchestrator.Shared.GameLogic.Recipes;

namespace KitchenOrchestrator.GameServer.Models
{
    /// <summary>
    /// Represents a plate as it accumulates ingredients on its way to delivery.
    /// A plate is created empty from a PlateSource and filled one ingredient at a time
    /// by AddToPlate actions at Counter stations.
    /// </summary>
    public class PlateState
    {
        public List<Ingredient> Contents { get; } = new();

        public bool IsEmpty => Contents.Count == 0;

        public void AddIngredient(Ingredient ingredient)
        {
            Contents.Add(ingredient);
        }
    }
}
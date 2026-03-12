using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;

namespace KitchenOrchestrator.GameServer.Models
{
    /// <summary>
    /// Represents a single ingredient item as it moves through the kitchen pipeline.
    /// Shared between ConnectedPlayer.HeldItem and StationState.HeldItem.
    /// It is the same object reference — when a player deposits it, the reference moves
    /// to the station; when they collect it, it moves back. No copying.
    /// </summary>
    public class HeldItem
    {
        public Ingredient Ingredient { get; }
        public ItemPrepState PrepState { get; set; }

        public HeldItem(Ingredient ingredient, ItemPrepState prepState = ItemPrepState.Raw)
        {
            Ingredient = ingredient;
            PrepState = prepState;
        }
    }
}
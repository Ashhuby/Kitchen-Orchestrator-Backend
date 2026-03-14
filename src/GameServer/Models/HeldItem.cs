using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;

namespace KitchenOrchestrator.GameServer.Models
{
    /// <summary>
    /// What a player or station is currently holding.
    /// Exactly one of Ingredient or Plate is set — never both, never neither
    /// (use null HeldItem to represent empty hands / empty station).
    /// </summary>
    public class HeldItem
    {
        // ── Ingredient path ───────────────────────────────────────────────────
        public Ingredient? Ingredient { get; }
        public ItemPrepState PrepState { get; set; }

        // ── Plate path ────────────────────────────────────────────────────────
        public PlateState? Plate { get; }

        public bool IsPlate => Plate != null;
        public bool IsIngredient => Ingredient.HasValue;

        /// <summary>Construct a held ingredient.</summary>
        public HeldItem(Ingredient ingredient, ItemPrepState prepState = ItemPrepState.Raw)
        {
            Ingredient = ingredient;
            PrepState  = prepState;
        }

        /// <summary>Construct a held plate (may be empty or already have contents).</summary>
        public HeldItem(PlateState plate)
        {
            Plate = plate;
        }
    }
}
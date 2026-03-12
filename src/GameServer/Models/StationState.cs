using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;

namespace KitchenOrchestrator.GameServer.Models
{
    /// <summary>
    /// Server-side authoritative state for a single station in a match.
    /// All mutation goes through this class — the hub and game loop read and write here.
    /// </summary>
    public class StationState
    {
        public string StationId { get; }
        public StationType Type { get; }

        // Only set for IngredientSource stations. Null for all others.
        public Ingredient? SourceIngredient { get; }

        // The item currently sitting on this station. Null if empty.
        public HeldItem? HeldItem { get; set; }

        // ChoppingBoard only — set to the PlayerId of whoever pressed BeginPrep.
        // Walking away calls CancelPrep which clears this and resets progress.
        public Guid? OccupyingPlayerId { get; set; }

        // Processing state (ChoppingBoard and Stove only)
        public float TotalDuration { get; private set; }
        public float TimeRemaining { get; private set; }
        public bool IsProcessing { get; private set; }

        // True when a timed process has finished but the item hasn't been collected yet.
        public bool IsComplete { get; private set; }

        // Burn grace period for Stove (seconds after IsComplete before item becomes Burned)
        private float _burnTimeRemaining;
        private const float BurnGracePeriod = 5f;

        public float ProgressNormalized =>
            TotalDuration > 0 ? 1f - (TimeRemaining / TotalDuration) : 0f;

        public StationState(string stationId, StationType type, Ingredient? sourceIngredient = null)
        {
            StationId = stationId;
            Type = type;
            SourceIngredient = sourceIngredient;
        }

        /// <summary>
        /// Starts a timed process. Called by hub on Deposit (Stove) or BeginPrep (ChoppingBoard).
        /// </summary>
        public void BeginProcessing(float duration)
        {
            TotalDuration = duration;
            TimeRemaining = duration;
            IsProcessing = true;
            IsComplete = false;
            _burnTimeRemaining = BurnGracePeriod;
        }

        /// <summary>
        /// Called by GameLoopService every tick. Returns true if state changed (needs broadcast).
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (!IsProcessing && !IsComplete) return false;

            if (IsProcessing)
            {
                TimeRemaining = Math.Max(0f, TimeRemaining - deltaTime);
                if (TimeRemaining <= 0f)
                {
                    IsProcessing = false;
                    IsComplete = true;

                    // Advance the item's prep state
                    if (HeldItem != null)
                    {
                        HeldItem.PrepState = Type == StationType.ChoppingBoard
                            ? ItemPrepState.Chopped
                            : ItemPrepState.Cooked;
                    }

                    return true; // State changed
                }
                return true; // Progress advancing — broadcast
            }

            // IsComplete — Stove burn countdown
            if (Type == StationType.Stove && HeldItem != null)
            {
                _burnTimeRemaining -= deltaTime;
                if (_burnTimeRemaining <= 0f)
                {
                    HeldItem.PrepState = ItemPrepState.Burned;
                    IsComplete = false; // Burned item stays on stove but can't be used
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resets all processing state. Called after Collect or CancelPrep.
        /// </summary>
        public void ResetProgress()
        {
            TotalDuration = 0f;
            TimeRemaining = 0f;
            IsProcessing = false;
            IsComplete = false;
            _burnTimeRemaining = 0f;
        }
    }
}
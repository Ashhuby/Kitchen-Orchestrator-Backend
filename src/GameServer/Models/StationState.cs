using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;

namespace KitchenOrchestrator.GameServer.Models
{
    /// <summary>
    /// Server-side state for a single station in a match.
    /// Lives inside MatchSession.Stations dictionary.
    /// The GameLoopService ticks timed stations (ChoppingBoard, Stove) each frame.
    /// </summary>
    public class StationState
    {
        public string StationId { get; }
        public StationType Type { get; }

        // What ingredient is currently on this station (null = empty)
        public HeldItem? HeldItem { get; set; }

        // --- Timed station fields (ChoppingBoard and Stove only) ---

        // How long this station takes to process its current item
        public float DurationSeconds { get; private set; }

        // How many seconds of progress have accumulated
        public float ProgressSeconds { get; set; }

        // Normalized 0→1 for client UI
        public float ProgressNormalized => DurationSeconds > 0
            ? Math.Clamp(ProgressSeconds / DurationSeconds, 0f, 1f)
            : 0f;

        public bool IsComplete => ProgressSeconds >= DurationSeconds && DurationSeconds > 0;

        // --- ChoppingBoard-specific: only the player who started can complete it ---
        // If they walk away (CancelPrep), progress resets to 0 and this clears.
        public Guid? OccupyingPlayerId { get; set; }

        // --- IngredientSource-specific: what ingredient this source dispenses ---
        public Ingredient? SourceIngredient { get; }

        public StationState(string stationId, StationType type, Ingredient? sourceIngredient = null)
        {
            StationId = stationId;
            Type = type;
            SourceIngredient = sourceIngredient;
        }

        /// <summary>
        /// Called when an item is deposited onto a timed station.
        /// Sets the duration based on type and starts progress at 0.
        /// </summary>
        public void BeginProcessing(float durationSeconds)
        {
            DurationSeconds = durationSeconds;
            ProgressSeconds = 0f;
        }

        /// <summary>
        /// Resets all timed state. Called when chopping is cancelled or item removed.
        /// </summary>
        public void ResetProgress()
        {
            ProgressSeconds = 0f;
            DurationSeconds = 0f;
            OccupyingPlayerId = null;
        }
    }
}
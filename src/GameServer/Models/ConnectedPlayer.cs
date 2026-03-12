using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Recipes;

namespace KitchenOrchestrator.GameServer.Models
{
    public class ConnectedPlayer
    {
        public string ConnectionId { get; set; } = string.Empty;
        public Guid PlayerId { get; }
        public string SteamId { get; } = string.Empty;
        public string DisplayName { get; } = string.Empty;
        public int Score { get; set; }
        public int OrdersDelivered { get; set; }
        public bool IsReady { get; set; }

        // Position — updated by client at 10Hz, read by GameLoopService for broadcast
        public float X { get; set; }
        public float Y { get; set; }

        // Item the player is currently holding — null means empty hands
        // The server is the authority on this; client reflects what server confirms
        public HeldItem? HeldItem { get; set; }

        public ConnectedPlayer(string connectionId, Guid playerId, string steamId, string displayName)
        {
            ConnectionId = connectionId;
            PlayerId = playerId;
            SteamId = steamId;
            DisplayName = displayName;
        }
    }

    // Represents an ingredient in a player's hands or on a station.
    // Ingredient is the base type; PrepState tracks how processed it is.
    public class HeldItem
    {
        public Ingredient Ingredient { get; set; }
        public ItemPrepState PrepState { get; set; } = ItemPrepState.Raw;

        public HeldItem(Ingredient ingredient, ItemPrepState prepState = ItemPrepState.Raw)
        {
            Ingredient = ingredient;
            PrepState = prepState;
        }
    }
}
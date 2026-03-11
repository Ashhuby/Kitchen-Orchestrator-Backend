namespace KitchenOrchestrator.Shared.Contracts.Enums
{
    public enum MatchState
    {
        Lobby = 0,
        Active = 1,
        Abandoned = 2,
        Completed = 3
    }

    public enum OrderStatus
    {
        Waiting = 0,
        InProgress = 1,
        Delivered = 2,
        TimedOut = 3
    }

    public enum PlayerConnectionState
    {
        ClientConnected = 0,
        ClientValidated = 1,
        JoiningLobby = 2,
        JoinedLobby = 3,
        Disconnected = 4
    }

    public enum PlayerAction
    {
        PickUpIngredient = 0,
        PlaceItem = 1,
        UseStation = 2,
        UseHeldItem = 3,
        DeliverDish = 4
    }

    // Discriminates what a player intends to do at a station.
    // The server resolves which is valid based on station type + player held item.
    public enum StationActionType
    {
        Pickup = 0,      // Ingredient source: give player raw ingredient
        Deposit = 1,     // Chopping board / stove: place held item onto station
        BeginPrep = 2,   // Chopping board: start timed prep (player must stay)
        CancelPrep = 3,  // Chopping board: player walks away — resets progress
        Collect = 4,     // Chopping board / stove: take finished item off station
        Deliver = 5      // Delivery counter: submit held item against active orders
    }

    // Tracks how far through the cooking pipeline an ingredient is.
    public enum ItemPrepState
    {
        Raw = 0,
        Chopped = 1,
        Cooked = 2,
        Burned = 3       // Overcooked hahahaha — item must be discarded, cannot be recovered
    }

    public enum StationType
    {
        IngredientSource = 0,
        ChoppingBoard = 1,
        Stove = 2,
        DeliveryCounter = 3
    }
}
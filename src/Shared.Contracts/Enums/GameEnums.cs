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
}
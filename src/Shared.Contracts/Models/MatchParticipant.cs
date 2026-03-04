namespace KitchenOrchestrator.Shared.Contracts.Models
{   
    public class MatchParticipant
    {
        public Guid Id {get; set;} = Guid.Empty;
        public Guid MatchHistoryId {get; set;} = Guid.Empty;
        public MatchHistory MatchHistory {get; set;} = null!;
        public Guid PlayerProfileId {get; set;} = Guid.Empty;
        public PlayerProfile PlayerProfile {get; set;} = null!;               
        public int IndividualScore {get; set;} = 0;
        public int OrdersDelivered  {get; set;} = 0;       
    }
}
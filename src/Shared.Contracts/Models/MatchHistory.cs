using KitchenOrchestrator.Shared.Contracts.Enums;
namespace KitchenOrchestrator.Shared.Contracts.Models
{
    public class MatchHistory
    {
        public Guid Id {get; set;} = Guid.Empty;
        public Guid MatchSessionId {get; set;} = Guid.Empty;
        public DateTime MatchBegin {get; set;} = DateTime.MinValue;
        public DateTime MatchEnd {get; set;} = DateTime.MinValue;
        public string LevelId {get; set;} = string.Empty;
        public int FinalScore {get; set;} = 0;
        public int TargetScore {get; set;} = 0;
        public MatchState FinalState {get; set;} = MatchState.Active;   // Uses the GameEnum we made eariler
        public int FailedOrders {get; set;} = 0;
        public int CompletedOrders {get; set;} = 0;
        public int PerfectOrders {get; set;} = 0;
        public ICollection<MatchParticipant> Participants {get; set;} = new List<MatchParticipant>();
    }   
}
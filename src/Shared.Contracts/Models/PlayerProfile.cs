namespace KitchenOrchestrator.Shared.Contracts.Models
{
    public class PlayerProfile
    {
        public Guid Id {get; set;} = Guid.Empty; // GUID non guessable ( Primary key for EF )
        public string SteamId {get; set;} = string.Empty; //17-digit number 
        public string DisplayName {get; set;} = string.Empty;
        public DateTime AccountCreatedUtc {get; set;} = DateTime.UtcNow;
        public DateTime LastLoggedInUtc {get; set;} = DateTime.MinValue;
        public int MatchesPlayed {get; set;} = 0;
        public int MatchesWon {get; set;} = 0;
        public int TotalScore {get; set;} = 0;
        public int PerfectOrders {get; set;} = 0; 
    }
}
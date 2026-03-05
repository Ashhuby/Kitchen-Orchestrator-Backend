using KitchenOrchestrator.Shared.Contracts.Enums;
namespace  KitchenOrchestrator.Shared.Contracts.Interfaces
{
    public interface IPlayerIdentity
    {
        public Guid Id { get; } // Get no set bc its read only
        public string SteamId { get; }
        public string DisplayName { get; }
    }   
    public interface IMatchResult
    {
        public Guid MatchSessionId { get; }
        public string LevelId { get; }
        public int FinalScore { get; }
        public int TargetScore { get; }
        public MatchState FinalState { get; }
        public bool IsWin => FinalScore >= TargetScore && FinalState == MatchState.Completed; 
    }
}

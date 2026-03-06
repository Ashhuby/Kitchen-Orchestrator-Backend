namespace KitchenOrchestrator.Shared.GameLogic.Levels{
    public class LevelDefinition{
        public string LevelId{ get; }   // Gets stored in MatchHistory.LevelId
        public string DisplayName { get; }
        public int TargetScore { get; }
        public float DurationSeconds { get; }
        public float OrderSpawnIntervalSeconds { get; }
        public int MaxSimultaneousOrders { get; }

        public LevelDefinition(string levelId, string displayName, int targetScore, float durationSeconds, float orderSpawnIntervalSeconds, int maxSimultaneousOrders)
        {
            LevelId = levelId;
            DisplayName = displayName;
            TargetScore = targetScore;
            DurationSeconds = durationSeconds;
            OrderSpawnIntervalSeconds = orderSpawnIntervalSeconds;
            MaxSimultaneousOrders = maxSimultaneousOrders;
        }

    }
}
namespace KitchenOrchestrator.Shared.GameLogic.Levels
{
    // Here lies the level definitions here ye here ye
    public static class LevelRegistry
    {
        public static IReadOnlyList<LevelDefinition> AllLevels { get; } = new List<LevelDefinition>
        {
            new LevelDefinition("Map1", "SaladBar", 600, 120f, 5, 5),
            new LevelDefinition("Map2", "SushiBar", 800, 120f, 3, 3),
            new LevelDefinition("Map3", "BurgerDiner", 1200, 120f, 5, 3)        
            
        };

        public static LevelDefinition? GetById(string id)
        {
            return AllLevels.FirstOrDefault(r => 
                r.LevelId.Equals(id, StringComparison.OrdinalIgnoreCase));
        }
    }   
}
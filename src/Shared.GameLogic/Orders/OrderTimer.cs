namespace KitchenOrchestrator.Shared.GameLogic.Orders
{
    public class OrderTimer
    {
        public float TotalDuration { get; }
        public float TimeRemaining { get; private set; }
        public bool IsExpired => TimeRemaining <= 0;

        public OrderTimer(float totalDuration)
        {
            TotalDuration = totalDuration;
            TimeRemaining = totalDuration;
        }

        public void Tick(float deltaTime)
        {
            TimeRemaining = Math.Max(0, TimeRemaining - deltaTime);
        }

    }
}
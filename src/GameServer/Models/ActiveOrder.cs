using KitchenOrchestrator.Shared.Contracts.Enums;
using KitchenOrchestrator.Shared.GameLogic.Orders;
using KitchenOrchestrator.Shared.GameLogic.Recipes;

namespace KitchenOrchestrator.GameServer.Models
{
    public class ActiveOrder
    {
        public Guid OrderId { get; }
        public RecipeDefinition Recipe { get; }
        public OrderTimer Timer { get; }
        public OrderStatus Status { get; set; }

        public ActiveOrder(RecipeDefinition recipe,float duration)
        {
            OrderId = Guid.NewGuid();
            Recipe = recipe;
            Timer = new OrderTimer(duration);
            Status = OrderStatus.Waiting;
        }
    }
}
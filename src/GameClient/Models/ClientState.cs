using KitchenOrchestrator.Shared.Contracts.DTOs;
namespace KitchenOrchestrator.GameClient.Models
{
    public class ClientState
    {
        public bool IsAuthenticated { get; set; }
        public string? Jwt { get; set; }
        public DateTime? TokenExpiresUtc { get; set; }
        public PlayerProfileDto? Profile { get; set; }
        public bool IsConnectedToMatch { get; set; }
        public Guid? CurrentSessionId { get; set; }
        public string? LevelId { get; set; }
        public Guid? PlayerId => Profile?.Id;
        public bool IsTokenValid => Jwt != null && TokenExpiresUtc.HasValue && TokenExpiresUtc.Value > DateTime.UtcNow;
    }
}
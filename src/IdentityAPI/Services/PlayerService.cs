using KitchenOrchestrator.Shared.Contracts.Models;
using KitchenOrchestrator.IdentityAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KitchenOrchestrator.IdentityAPI.Services
{
    public interface IPlayerService
    {
        Task<PlayerProfile> GetOrCreatePlayerAsync(string steamId, string displayName);
        Task<PlayerProfile?> GetByIdAsync(Guid id);
    }

    public class PlayerService : IPlayerService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<PlayerService> _logger;

        public PlayerService(AppDbContext db, ILogger<PlayerService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<PlayerProfile> GetOrCreatePlayerAsync(string steamId, string displayName)
        {
            try
            {
                // Check if the player already exists in our database
                var player = await _db.PlayerProfiles
                    .FirstOrDefaultAsync(p => p.SteamId == steamId);

                var now = DateTime.UtcNow;

                if (player != null)
                {
                    // Existing Player: Update their volatile info
                    _logger.LogInformation("Returning existing player: {SteamId}", steamId);
                    player.DisplayName = displayName;   // Steam usernames always change right? brb lemme check this 
                    player.LastLoggedInUtc = now; 
                }
                else
                {
                    // New Player: Initialize a fresh profile
                    _logger.LogInformation("Creating new player profile for SteamId: {SteamId}", steamId);
                    player = new PlayerProfile
                    {
                        // ID is set in postgres so we dont need to do it here 
                        SteamId = steamId,
                        DisplayName = displayName,
                        AccountCreatedUtc = now,
                        LastLoggedInUtc = now
                    };
                    _db.PlayerProfiles.Add(player);
                }

                // Commit changes to PostgreSQL
                await _db.SaveChangesAsync();
                return player;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database failure while getting/creating player for SteamId: {SteamId}", steamId);
                // We rethrow because the Auth process cannot continue if the DB is down.
                throw; 
            }
        }

        public async Task<PlayerProfile?> GetByIdAsync(Guid id)
        {            
            return await _db.PlayerProfiles.FindAsync(id);
        }
    }
}
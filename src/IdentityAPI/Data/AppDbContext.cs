using KitchenOrchestrator.Shared.Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace KitchenOrchestrator.IdentityAPI.Data
{
    public class AppDbContext : DbContext
    {
        // Standard Constructor for Dependency Injection
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // My Database Tables (DbSets)
        public DbSet<PlayerProfile> PlayerProfiles { get; set; } = null!;
        public DbSet<MatchHistory> MatchHistories { get; set; } = null!;
        public DbSet<MatchParticipant> MatchParticipants { get; set; } = null!;

        // Wiring up the Fluent API Configurations
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This one line replaces three manual calls. It finds PlayerProfileConfiguration, MatchHistoryConfiguration, etc.             
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
using KitchenOrchestrator.Shared.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KitchenOrchestrator.IdentityAPI.Data.Configurations
{
    public class MatchHistoryConfiguration : IEntityTypeConfiguration<MatchHistory>
    {
        public void Configure(EntityTypeBuilder<MatchHistory> builder)
        {
            // Primary Key
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

            // The Idempotency Guard: Unique MatchSessionId
            builder.Property(m => m.MatchSessionId).IsRequired();
            builder.HasIndex(m => m.MatchSessionId).IsUnique();

            // Level and Scoring
            builder.Property(m => m.LevelId).IsRequired().HasMaxLength(50);
            builder.Property(m => m.FinalScore).IsRequired();
            builder.Property(m => m.TargetScore).IsRequired();

            // Enum Mapping: Store 'Won/Lost/Quit' as Strings
            builder.Property(m => m.FinalState)
                .IsRequired()
                .HasConversion<string>();

            // Timestamps with Timezone awareness
            builder.Property(m => m.MatchBeginUtc).IsRequired().HasColumnType("timestamptz");
            builder.Property(m => m.MatchEndUtc).IsRequired().HasColumnType("timestamptz");

            // Relationship: One Match has Many Participants
            builder.HasMany(m => m.Participants)
                .WithOne(p => p.MatchHistory)
                .HasForeignKey(p => p.MatchHistoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
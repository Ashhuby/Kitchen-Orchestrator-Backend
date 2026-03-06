using KitchenOrchestrator.Shared.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KitchenOrchestrator.IdentityAPI.Data.Configurations
{
    public class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
    {
        public void Configure(EntityTypeBuilder<PlayerProfile> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            builder.Property(p => p.SteamId)
                .IsRequired()
                .HasMaxLength(20);

            builder.HasIndex(p => p.SteamId)
                .IsUnique();

            builder.Property(p => p.DisplayName)
                .IsRequired()
                .HasMaxLength(64);

            // Mapping to 'AccountCreated' with Postgres timestamptz
            builder.Property(p => p.AccountCreatedUtc)
                .IsRequired()
                .HasColumnType("timestamptz");

            // Mapping 'LastLoggedIn' with Postgres timestamptz
            builder.Property(p => p.LastLoggedInUtc)
                .IsRequired()
                .HasColumnType("timestamptz");
        }
    }
}
using KitchenOrchestrator.Shared.Contracts.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KitchenOrchestrator.IdentityAPI.Data.Configurations
{
    public class MatchParticipantConfiguration : IEntityTypeConfiguration<MatchParticipant>
    {
        public void Configure(EntityTypeBuilder<MatchParticipant> builder)
        {
            // Primary Key
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            // Performance Stats
            builder.Property(p => p.IndividualScore)
                .IsRequired();

            builder.Property(p => p.OrdersDelivered)
                .IsRequired();

            // Relationship: Linking to the Player
            // WithMany() is empty because PlayerProfile doesn't have a List<MatchParticipant>
            builder.HasOne(p => p.PlayerProfile)
                .WithMany() 
                .HasForeignKey(p => p.PlayerProfileId)
                .OnDelete(DeleteBehavior.Restrict); 
        }
    }
}
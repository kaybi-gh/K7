using System.Text.Json;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class UserScrobblerAccountConfiguration : IEntityTypeConfiguration<UserScrobblerAccount>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<UserScrobblerAccount> builder)
    {
        builder.Property(a => a.ConfigJson).IsRequired();
        builder.Property(a => a.DisplayName).HasMaxLength(200);

        builder.Property(a => a.MediaTypes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<MediaType>>(v, JsonOptions) ?? new List<MediaType>())
            .HasColumnType("text")
            .Metadata.SetValueComparer(new ValueComparer<List<MediaType>>(
                (a, b) => a!.SequenceEqual(b!),
                c => c.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                c => c.ToList()));

        builder.HasIndex(a => new { a.UserId, a.Provider });
        builder.HasIndex(a => a.UserId);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

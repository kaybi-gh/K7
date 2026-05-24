using System.Text.Json;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SmartPlaylistConfiguration : IEntityTypeConfiguration<SmartPlaylist>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<SmartPlaylist> builder)
    {
        builder.Property(p => p.RuleFilter)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<RuleGroup>(v, JsonOptions)!);
    }
}

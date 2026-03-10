using K7.Server.Domain.Entities.Playlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class SmartPlaylistConfiguration : IEntityTypeConfiguration<SmartPlaylist>
{
    public void Configure(EntityTypeBuilder<SmartPlaylist> builder)
    {
        builder.OwnsMany(p => p.Rules, rules =>
        {
            rules.ToJson();
        });
    }
}

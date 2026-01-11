using K7.Server.Domain.Entities.Devices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace K7.Server.Infrastructure.Database.Context.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.OwnsOne(x => x.NativeDeviceDetails);
        builder.OwnsOne(x => x.WebDeviceDetails);
        builder.OwnsOne(x => x.PlaybackCapabilities, navigationBuilder =>
        {
            navigationBuilder.Ignore(x => x.SupportedMediaFormats);
        });
    }
}

using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.UnitTests.Entities.Medias;

public class SerieApplyMetadataTests
{
    [Test]
    public void ApplyMetadata_ShouldRespectLocksAndPreserveFederationIds()
    {
        var serie = new Serie
        {
            Title = "Locked",
            Status = "Ended"
        };
        serie.LockField(nameof(Serie.Title));
        serie.ExternalIds.Add(new ExternalId { ProviderName = "federation", Value = "remote:9" });

        serie.ApplyMetadata(new ExternalSerieMetadata
        {
            Title = "New Title",
            Status = "Returning",
            Overview = "Fresh",
            ExternalIds =
            [
                new ExternalId { ProviderName = "tvdb", Value = "42" }
            ]
        });

        serie.Title.Should().Be("Locked");
        serie.Status.Should().Be("Returning");
        serie.Overview.Should().Be("Fresh");
        serie.ExternalIds.Should().ContainSingle(e => e.ProviderName == "tvdb");
        serie.ExternalIds.Should().ContainSingle(e => e.ProviderName == "federation");
    }
}

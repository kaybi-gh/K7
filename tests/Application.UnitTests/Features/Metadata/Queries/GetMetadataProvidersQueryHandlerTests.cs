using K7.Server.Application.Features.Metadata.Queries.GetMetadataProviders;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.UnitTests.Features.Metadata.Queries;

[TestFixture]
public class GetMetadataProvidersQueryHandlerTests
{
    [Test]
    public async Task Handle_ShouldPreferTvdbFirst_ForSerieLibraries()
    {
        IMetadataProviderInfo[] providers =
        [
            new StubProvider("tmdb", [LibraryMediaType.Movie, LibraryMediaType.Serie]),
            new StubProvider("tvdb", [LibraryMediaType.Serie])
        ];

        var handler = new GetMetadataProvidersQueryHandler(providers);
        var result = (await handler.Handle(new GetMetadataProvidersQuery { MediaType = LibraryMediaType.Serie }, CancellationToken.None)).ToList();

        result.Should().HaveCount(2);
        result[0].ProviderName.Should().Be("tvdb");
        result[1].ProviderName.Should().Be("tmdb");
    }

    private sealed class StubProvider(string providerName, IReadOnlyList<LibraryMediaType> supportedMediaTypes) : IMetadataProviderInfo
    {
        public string ProviderName { get; } = providerName;

        public IReadOnlyList<LibraryMediaType> SupportedMediaTypes { get; } = supportedMediaTypes;
    }
}

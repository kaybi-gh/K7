using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class ImageProcessorSvgTests
{
    private readonly ImageProcessor _processor = new();
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"k7-svg-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public async Task RasterizeSvgToWebPAsync_ShouldCreateWebPVariant()
    {
        var svgPath = Path.Combine(_tempDirectory, "logo.svg");
        var webpPath = Path.Combine(_tempDirectory, "logo.webp");
        await File.WriteAllTextAsync(svgPath,
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 50">
              <rect width="100" height="50" fill="#CC7A3E"/>
            </svg>
            """);

        await _processor.RasterizeSvgToWebPAsync(svgPath, webpPath, 200, quality: 85);

        File.Exists(webpPath).Should().BeTrue();
        new FileInfo(webpPath).Length.Should().BeGreaterThan(0);
        _processor.IsSvgFile(svgPath).Should().BeTrue();
    }

    [Test]
    public async Task ExtractDominantColorAsync_ShouldWork_ForSvg()
    {
        var svgPath = Path.Combine(_tempDirectory, "color.svg");
        await File.WriteAllTextAsync(svgPath,
            """
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10">
              <rect width="10" height="10" fill="#CC7A3E"/>
            </svg>
            """);

        var color = await _processor.ExtractDominantColorAsync(svgPath);

        color.Should().NotBeNullOrWhiteSpace();
    }
}

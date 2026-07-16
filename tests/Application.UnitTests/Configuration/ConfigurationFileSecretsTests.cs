using K7.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace K7.Server.Application.UnitTests.Configuration;

[TestFixture]
public class ConfigurationFileSecretsTests
{
    [Test]
    public void AddFileSecretOverrides_ShouldSetParentKey_WhenFileKeyPointsToExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"k7-secret-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "s3cret-value\n");

        try
        {
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Password:File"] = path,
                ["Database:UserID"] = "postgres"
            });

            configuration.AddFileSecretOverrides();

            configuration["Database:Password"].Should().Be("s3cret-value");
            configuration["Database:UserID"].Should().Be("postgres");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AddFileSecretOverrides_ShouldThrow_WhenFileMissing()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Password:File"] = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.txt")
        });

        var act = () => configuration.AddFileSecretOverrides();

        act.Should().Throw<FileNotFoundException>();
    }
}

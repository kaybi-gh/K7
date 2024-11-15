using MediaServer.Tests.Helpers.Helpers;

namespace MediaServer.Tests.Helpers.Fixtures;

[TestFixture]
public class FileFixture
{
    [OneTimeSetUp]
    protected void FileFixure_RunBeforeAnyTests() => FileHelper.CreateTestDirectory();

    [OneTimeTearDown]
    protected void FileFixure_RunAfterAnyTests() => FileHelper.DeleteTestDirectory();

    [SetUp]
    public void FileFixure_SetUp()
    {
        FileHelper.DeleteTestDirectory();
        FileHelper.CreateTestDirectory();
    }
}

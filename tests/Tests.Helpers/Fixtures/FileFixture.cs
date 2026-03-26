using K7.Tests.Helpers.Helpers;

namespace K7.Tests.Helpers.Fixtures;

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

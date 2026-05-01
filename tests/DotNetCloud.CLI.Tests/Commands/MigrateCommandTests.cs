using DotNetCloud.CLI.Commands;

namespace DotNetCloud.CLI.Tests.Commands;

[TestClass]
public class MigrateCommandTests
{
    [TestMethod]
    public void Create_HasExpectedNameAndDescription()
    {
        var command = MigrateCommand.Create();

        Assert.AreEqual("migrate", command.Name);
        Assert.IsNotNull(command.Description);
        Assert.IsTrue(command.Description.Contains("migration"), "Expected 'migration' in description");
    }
}

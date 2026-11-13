using DotNetCloud.Core.Server.Sandbox;

namespace DotNetCloud.Core.Server.Tests.Sandbox;

/// <summary>
/// Tests for the provisioning prerequisite remediation text: the statement an operator is told to
/// run must name the actual role/database, otherwise it cannot be pasted verbatim.
/// </summary>
[TestClass]
public class ModuleDataStorePrerequisiteMessageTests
{
    [TestMethod]
    public void DescribePostgreSqlRolePrerequisite_KnownRole_NamesQuotedRoleInStatement()
    {
        var message = ModuleDataStoreProvisioningService.DescribePostgreSqlRolePrerequisite("dotnetcloud");

        StringAssert.Contains(message, "CREATEROLE");
        StringAssert.Contains(message, "ALTER ROLE \"dotnetcloud\" CREATEROLE;");
        Assert.IsFalse(message.Contains("<dotnetcloud-role>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DescribePostgreSqlRolePrerequisite_UnknownRole_UsesPlaceholder()
    {
        var message = ModuleDataStoreProvisioningService.DescribePostgreSqlRolePrerequisite(null);

        StringAssert.Contains(message, "ALTER ROLE <dotnetcloud-role> CREATEROLE;");
    }

    [TestMethod]
    public void DescribePostgreSqlRolePrerequisite_WhitespaceRole_UsesPlaceholder()
    {
        var message = ModuleDataStoreProvisioningService.DescribePostgreSqlRolePrerequisite("   ");

        StringAssert.Contains(message, "<dotnetcloud-role>");
    }

    [TestMethod]
    public void DescribePostgreSqlRolePrerequisite_RoleWithQuote_EscapesIdentifier()
    {
        var message = ModuleDataStoreProvisioningService.DescribePostgreSqlRolePrerequisite("we\"ird");

        StringAssert.Contains(message, "ALTER ROLE \"we\"\"ird\" CREATEROLE;");
    }

    [TestMethod]
    public void DescribeSqlServerContainmentPrerequisite_KnownDatabase_NamesQuotedDatabase()
    {
        var message = ModuleDataStoreProvisioningService.DescribeSqlServerContainmentPrerequisite("dotnetcloud");

        StringAssert.Contains(message, "ALTER DATABASE [dotnetcloud] SET CONTAINMENT = PARTIAL;");
    }

    [TestMethod]
    public void DescribeSqlServerContainmentPrerequisite_UnknownDatabase_UsesDefaultName()
    {
        var message = ModuleDataStoreProvisioningService.DescribeSqlServerContainmentPrerequisite(string.Empty);

        StringAssert.Contains(message, "ALTER DATABASE [dotnetcloud] SET CONTAINMENT = PARTIAL;");
    }
}

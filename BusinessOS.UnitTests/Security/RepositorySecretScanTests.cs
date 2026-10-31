using FluentAssertions;

namespace BusinessOS.UnitTests.Security;

public class RepositorySecretScanTests
{
    [Fact]
    public void CommittedConfiguration_DoesNotContainKnownSecrets()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "BusinessOS.API", "appsettings.json"),
            Path.Combine(root, "BusinessOS.API", "appsettings.Development.json"),
            Path.Combine(root, "BusinessOS.API", "appsettings.Production.json"),
            Path.Combine(root, "BusinessOS.Infrastructure", "Data", "DevSeeder.cs"),
            Path.Combine(root, "..", "BusinessOS_UI", "src", "environments", "environment.ts"),
            Path.Combine(root, "..", "BusinessOS_UI", "src", "environments", "environment.prod.ts")
        };

        var forbidden = new[]
        {
            "Admin123!",
            "THIS_IS_SUPER_SECRET_KEY_123456789",
            "BEGIN PRIVATE KEY",
            "sk_live_",
            "Password: {Password}"
        };

        foreach (var file in files)
        {
            File.Exists(file).Should().BeTrue($"expected {file} to exist");
            var text = File.ReadAllText(file);
            foreach (var secret in forbidden)
            {
                text.Should().NotContain(secret, $"{file} still contains a known secret marker");
            }
        }

        var appsettings = File.ReadAllText(Path.Combine(root, "BusinessOS.API", "appsettings.json"));
        appsettings.Should().NotContain("\"Key\"");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "BusinessOS.API", "appsettings.json");
            if (File.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the BusinessOS.API project from the test output directory.");
    }
}

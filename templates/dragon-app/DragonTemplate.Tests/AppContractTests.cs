using DragonTemplate.Application;
using DragonTemplate.Infrastructure;
using Xunit;

namespace DragonTemplate.Tests;

public sealed class AppContractTests
{
    [Fact]
    public void AppInfoProvider_ExposesConfiguredIdentity()
    {
        var provider = new AppInfoProvider();

        Assert.Equal("Dragon Template", provider.Current.ProductName);
        Assert.Equal("com.dragontemplate.app", provider.Current.AppId);
        Assert.Equal("DRAGON_TEMPLATE", provider.Current.EnvPrefix);
    }

    [Fact]
    public void LocalDataPathProvider_UsesEnvironmentOverride()
    {
        var previous = Environment.GetEnvironmentVariable("DRAGON_TEMPLATE_DATABASE_PATH");
        var expected = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "template.db");

        try
        {
            Environment.SetEnvironmentVariable("DRAGON_TEMPLATE_DATABASE_PATH", expected);
            ILocalDataPathProvider provider = new LocalDataPathProvider();

            Assert.Equal(Path.GetFullPath(expected), provider.DatabasePath);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DRAGON_TEMPLATE_DATABASE_PATH", previous);
        }
    }
}

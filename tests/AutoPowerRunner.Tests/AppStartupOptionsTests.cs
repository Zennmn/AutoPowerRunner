using AutoPowerRunner;

namespace AutoPowerRunner.Tests;

public sealed class AppStartupOptionsTests
{
    [Fact]
    public void ShouldShowMainWindow_WhenNoSilentStartupArgument_ReturnsTrue()
    {
        Assert.True(App.ShouldShowMainWindow([]));
    }

    [Fact]
    public void ShouldShowMainWindow_WhenSilentStartupArgumentPresent_ReturnsFalse()
    {
        Assert.False(App.ShouldShowMainWindow(["--silent-startup"]));
    }

    [Fact]
    public void ShouldShowMainWindow_WhenSilentStartupArgumentUsesDifferentCasing_ReturnsFalse()
    {
        Assert.False(App.ShouldShowMainWindow(["--SILENT-STARTUP"]));
    }

    [Theory]
    [InlineData(false, "开启管理员自启")]
    [InlineData(true, "关闭管理员自启")]
    public void BuildTrayAutostartMenuText_ReturnsStateSpecificAction(bool isEnabled, string expected)
    {
        Assert.Equal(expected, App.BuildTrayAutostartMenuText(isEnabled));
    }

    [Fact]
    public void GetAuthorizedConfigHash_ReturnsValueFollowingArgument()
    {
        Assert.Equal("ABC123", App.GetAuthorizedConfigHash(["--silent-startup", "--authorized-config-hash", "ABC123"]));
        Assert.Null(App.GetAuthorizedConfigHash(["--silent-startup"]));
    }

    [Fact]
    public void ShouldRunEnabledTasks_RequiresSilentStartupAndAuthorizationHash()
    {
        Assert.False(App.ShouldRunEnabledTasks([]));
        Assert.False(App.ShouldRunEnabledTasks(["--silent-startup"]));
        Assert.True(App.ShouldRunEnabledTasks(["--silent-startup", "--authorized-config-hash", "ABC123"]));
    }
}

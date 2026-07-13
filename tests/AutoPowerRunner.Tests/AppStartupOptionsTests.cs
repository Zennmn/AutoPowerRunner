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
}

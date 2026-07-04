using System.Drawing;
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class ManagerWindowLayoutStateTests
{
    private static readonly Rectangle Screen = new(0, 0, 1920, 1080);

    [Fact]
    public void FromConfig_UsesDefaultsWhenSavedSizeIsTooSmall()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 800,
            ManagerWindowHeight = 500,
            ManagerWindowLeft = 100,
            ManagerWindowTop = 100,
            ManagerSplitterDistance = 300
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(1120, state.Width);
        Assert.Equal(720, state.Height);
        Assert.False(state.HasSavedBounds);
    }

    [Fact]
    public void FromConfig_UsesDefaultsWhenSavedBoundsAreOffScreen()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 1120,
            ManagerWindowHeight = 720,
            ManagerWindowLeft = 5000,
            ManagerWindowTop = 5000,
            ManagerSplitterDistance = 420
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(1120, state.Width);
        Assert.Equal(720, state.Height);
        Assert.False(state.HasSavedBounds);
    }

    [Fact]
    public void FromConfig_AcceptsValidSavedBounds()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 1200,
            ManagerWindowHeight = 800,
            ManagerWindowLeft = 120,
            ManagerWindowTop = 80,
            ManagerSplitterDistance = 500
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(1200, state.Width);
        Assert.Equal(800, state.Height);
        Assert.Equal(120, state.Left);
        Assert.Equal(80, state.Top);
        Assert.Equal(500, state.SplitterDistance);
        Assert.True(state.HasSavedBounds);
    }

    [Fact]
    public void FromConfig_ClampsSplitterToKeepBothPanesUsable()
    {
        var config = new StartupConfig
        {
            ManagerWindowWidth = 920,
            ManagerWindowHeight = 650,
            ManagerWindowLeft = 20,
            ManagerWindowTop = 20,
            ManagerSplitterDistance = 800
        };

        var state = ManagerWindowLayoutState.FromConfig(config, [Screen]);

        Assert.Equal(640, state.SplitterDistance);
    }
}

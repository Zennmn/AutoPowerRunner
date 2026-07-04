using System.Drawing;

namespace AutoElevateLauncher;

internal sealed record ManagerWindowLayoutState(int Width, int Height, int Left, int Top, int SplitterDistance, bool HasSavedBounds)
{
    public const int DefaultWidth = 1120;
    public const int DefaultHeight = 720;
    public const int MinimumWidth = 920;
    public const int MinimumHeight = 600;
    public const int MinimumPaneWidth = 280;
    public const int DefaultSplitterDistance = 520;

    public static ManagerWindowLayoutState FromConfig(StartupConfig config, IReadOnlyList<Rectangle> workingAreas)
    {
        var width = config.ManagerWindowWidth.GetValueOrDefault(DefaultWidth);
        var height = config.ManagerWindowHeight.GetValueOrDefault(DefaultHeight);
        var left = config.ManagerWindowLeft.GetValueOrDefault(0);
        var top = config.ManagerWindowTop.GetValueOrDefault(0);

        var hasValidSize = width >= MinimumWidth && height >= MinimumHeight;
        var savedBounds = new Rectangle(left, top, width, height);
        var hasValidBounds = hasValidSize && workingAreas.Any(area => area.IntersectsWith(savedBounds));

        if (!hasValidBounds)
        {
            width = DefaultWidth;
            height = DefaultHeight;
            left = 0;
            top = 0;
        }

        var splitter = ClampSplitter(width, config.ManagerSplitterDistance.GetValueOrDefault(DefaultSplitterDistance));
        return new ManagerWindowLayoutState(width, height, left, top, splitter, hasValidBounds);
    }

    public static void SaveToConfig(StartupConfig config, Form form, SplitContainer split)
    {
        if (form.WindowState == FormWindowState.Normal)
        {
            config.ManagerWindowWidth = form.Width;
            config.ManagerWindowHeight = form.Height;
            config.ManagerWindowLeft = form.Left;
            config.ManagerWindowTop = form.Top;
        }

        config.ManagerSplitterDistance = split.SplitterDistance;
    }

    private static int ClampSplitter(int width, int splitterDistance)
    {
        var max = Math.Max(MinimumPaneWidth, width - MinimumPaneWidth);
        return Math.Min(Math.Max(splitterDistance, MinimumPaneWidth), max);
    }
}

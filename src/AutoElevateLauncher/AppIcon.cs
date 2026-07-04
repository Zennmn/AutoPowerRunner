using System.Drawing;

namespace AutoElevateLauncher;

internal static class AppIcon
{
    public static Icon Load()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            return File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }
}

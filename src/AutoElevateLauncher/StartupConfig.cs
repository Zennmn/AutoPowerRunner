namespace AutoElevateLauncher;

public sealed class StartupConfig
{
    public List<StartupItem> Items { get; set; } = [];
    public bool StartManagerAtLogin { get; set; } = false;
    public int? ManagerWindowWidth { get; set; }
    public int? ManagerWindowHeight { get; set; }
    public int? ManagerWindowLeft { get; set; }
    public int? ManagerWindowTop { get; set; }
    public int? ManagerSplitterDistance { get; set; }
}

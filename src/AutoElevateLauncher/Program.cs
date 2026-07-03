namespace AutoElevateLauncher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--run-item")
        {
            return new ItemRunner(new ConfigStore()).RunAsync(args[1]).GetAwaiter().GetResult();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new ManagerContext());
        return 0;
    }
}
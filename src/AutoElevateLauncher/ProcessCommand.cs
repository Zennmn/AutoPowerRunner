namespace AutoElevateLauncher;

public sealed record ProcessCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}
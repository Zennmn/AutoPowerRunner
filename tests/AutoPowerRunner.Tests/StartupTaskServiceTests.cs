using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class StartupTaskServiceTests
{
    [Fact]
    public void BuildCreateArguments_UsesCurrentUserLogonAndHighestPrivilege()
    {
        var args = StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: @"DESKTOP\User");

        Assert.Contains("/Create", args);
        Assert.Contains("/TN \"AutoPowerRunner\"", args);
        Assert.Contains("/SC ONLOGON", args);
        Assert.Contains("/RL HIGHEST", args);
        Assert.Contains("/RU \"DESKTOP\\User\"", args);
        Assert.Contains("/TR \"\\\"C:\\Program Files\\AutoPowerRunner\\AutoPowerRunner.exe\\\"\"", args);
        Assert.Contains("/F", args);
    }

    [Fact]
    public void BuildDeleteArguments_ForcesDeletion()
    {
        var args = StartupTaskService.BuildDeleteArguments("AutoPowerRunner");

        Assert.Equal("/Delete /TN \"AutoPowerRunner\" /F", args);
    }
}

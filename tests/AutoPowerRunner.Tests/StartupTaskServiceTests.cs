using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class StartupTaskServiceTests
{
    [Fact]
    public void BuildCreateArguments_ReturnsExactExpectedCommand()
    {
        var args = StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: @"DESKTOP\User");

        Assert.Equal(
            "/Create /TN \"AutoPowerRunner\" /SC ONLOGON /RL HIGHEST /RU \"DESKTOP\\User\" /TR \"\\\"C:\\Program Files\\AutoPowerRunner\\AutoPowerRunner.exe\\\"\" /F",
            args);
    }

    [Fact]
    public void BuildDeleteArguments_ForcesDeletion()
    {
        var args = StartupTaskService.BuildDeleteArguments("AutoPowerRunner");

        Assert.Equal("/Delete /TN \"AutoPowerRunner\" /F", args);
    }

    [Fact]
    public void BuildQueryArguments_BuildsQueryCommand()
    {
        var args = StartupTaskService.BuildQueryArguments("AutoPowerRunner");

        Assert.Equal("/Query /TN \"AutoPowerRunner\"", args);
    }

    [Fact]
    public void BuildQueryVerboseArguments_BuildsVerboseListQueryCommand()
    {
        var args = StartupTaskService.BuildQueryVerboseArguments("AutoPowerRunner");

        Assert.Equal("/Query /TN \"AutoPowerRunner\" /V /FO LIST", args);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputMatchesExecutablePathAndIsNotDisabled_ReturnsTrue()
    {
        var executablePath = @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe";
        var commands = new List<(string Arguments, bool RunAsAdmin)>();
        var service = new StartupTaskService(
            executablePath,
            commandRunner: (arguments, runAsAdmin) =>
            {
                commands.Add((arguments, runAsAdmin));
                return new StartupTaskService.CommandResult(0, $"""
                    Folder: \
                    TaskName: AutoPowerRunner
                    Task To Run: "{executablePath}"
                    Status: Ready
                    Run Level: Highest
                    """, "");
            });

        var enabled = service.IsEnabled();

        Assert.True(enabled);
        Assert.Equal([(StartupTaskService.BuildQueryVerboseArguments("AutoPowerRunner"), false)], commands);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputContainsOldExecutablePath_ReturnsFalse()
    {
        var service = new StartupTaskService(
            @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, """
                Folder: \
                TaskName: AutoPowerRunner
                Task To Run: "C:\Old\AutoPowerRunner.exe"
                Status: Ready
                Run Level: Highest
                """, ""));

        var enabled = service.IsEnabled();

        Assert.False(enabled);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputStatusIsDisabled_ReturnsFalse()
    {
        var executablePath = @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe";
        var service = new StartupTaskService(
            executablePath,
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, $"""
                Folder: \
                TaskName: AutoPowerRunner
                Task To Run: "{executablePath}"
                Status: Disabled
                Run Level: Highest
                """, ""));

        var enabled = service.IsEnabled();

        Assert.False(enabled);
    }

    [Fact]
    public void BuildCreateArguments_RejectsQuotesInTaskName()
    {
        var exception = Assert.Throws<ArgumentException>(() => StartupTaskService.BuildCreateArguments(
            taskName: "Auto\"PowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: @"DESKTOP\User"));

        Assert.Equal("taskName", exception.ParamName);
    }

    [Fact]
    public void BuildCreateArguments_RejectsQuotesInExecutablePath()
    {
        var exception = Assert.Throws<ArgumentException>(() => StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: "C:\\Program Files\\AutoPowerRunner\\Auto\"PowerRunner.exe",
            userName: @"DESKTOP\User"));

        Assert.Equal("executablePath", exception.ParamName);
    }

    [Fact]
    public void BuildCreateArguments_RejectsQuotesInUserName()
    {
        var exception = Assert.Throws<ArgumentException>(() => StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: "DESKTOP\\Us\"er"));

        Assert.Equal("userName", exception.ParamName);
    }

    [Fact]
    public void GetSchtasksPath_UsesWindowsSystem32()
    {
        var path = StartupTaskService.GetSchtasksPath();
        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Assert.EndsWith(Path.Combine("System32", "schtasks.exe"), path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(windowsFolder, path, StringComparison.OrdinalIgnoreCase);
    }
}

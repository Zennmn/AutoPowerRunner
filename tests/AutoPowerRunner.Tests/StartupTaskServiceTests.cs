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
            "/Create /TN \"AutoPowerRunner\" /SC ONLOGON /RL HIGHEST /RU \"DESKTOP\\User\" /TR \"\\\"C:\\Program Files\\AutoPowerRunner\\AutoPowerRunner.exe\\\" --silent-startup\" /F",
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
                    Task To Run: "{executablePath}" --silent-startup
                    Status: Ready
                    Run Level: Highest
                    """, "");
            });

        var enabled = service.IsEnabled();

        Assert.True(enabled);
        Assert.Equal([(StartupTaskService.BuildQueryXmlArguments("AutoPowerRunner"), false)], commands);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputHasQuotedExecutablePathWithArguments_ReturnsTrue()
    {
        var executablePath = @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe";
        var service = new StartupTaskService(
            executablePath,
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, $"""
                Folder: \
                TaskName: AutoPowerRunner
                Task To Run: "{executablePath}" --silent-startup --some-arg
                Status: Ready
                Run Level: Highest
                """, ""));

        var enabled = service.IsEnabled();

        Assert.True(enabled);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputExecutablePathHasDifferentCasing_ReturnsTrue()
    {
        var service = new StartupTaskService(
            @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, """
                Folder: \
                TaskName: AutoPowerRunner
                Task To Run: "c:\program files\autopowerrunner\autopowerrunner.exe" --silent-startup
                Status: Ready
                Run Level: Highest
                """, ""));

        var enabled = service.IsEnabled();

        Assert.True(enabled);
    }

    [Fact]
    public void IsEnabled_WhenXmlOutputMatchesExecutablePathAndSilentArgument_ReturnsTrue()
    {
        var executablePath = @"C:\Tools\AutoPowerRunner\AutoPowerRunner.exe";
        var service = new StartupTaskService(
            executablePath,
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, """
                <?xml version="1.0" encoding="UTF-16"?>
                <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
                  <Principals>
                    <Principal id="Author">
                      <RunLevel>HighestAvailable</RunLevel>
                    </Principal>
                  </Principals>
                  <Settings>
                    <Enabled>true</Enabled>
                  </Settings>
                  <Actions Context="Author">
                    <Exec>
                      <Command>"C:\Tools\AutoPowerRunner\AutoPowerRunner.exe"</Command>
                      <Arguments>--silent-startup</Arguments>
                    </Exec>
                  </Actions>
                </Task>
                """, ""));

        var enabled = service.IsEnabled();

        Assert.True(enabled);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputExecutablePathHasOldExtension_ReturnsFalse()
    {
        var service = new StartupTaskService(
            @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, """
                Folder: \
                TaskName: AutoPowerRunner
                Task To Run: "C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe.old"
                Status: Ready
                Run Level: Highest
                """, ""));

        var enabled = service.IsEnabled();

        Assert.False(enabled);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputWrapsExecutablePathInCommandShell_ReturnsFalse()
    {
        var service = new StartupTaskService(
            @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, """
                Folder: \
                TaskName: AutoPowerRunner
                Task To Run: cmd.exe /c "C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe"
                Status: Ready
                Run Level: Highest
                """, ""));

        var enabled = service.IsEnabled();

        Assert.False(enabled);
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
                Task To Run: "{executablePath}" --silent-startup
                Status: Disabled
                Run Level: Highest
                """, ""));

        var enabled = service.IsEnabled();

        Assert.False(enabled);
    }

    [Fact]
    public void IsEnabled_WhenVerboseOutputMatchesExecutablePathButMissingSilentStartupArgument_ReturnsFalse()
    {
        var executablePath = @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe";
        var service = new StartupTaskService(
            executablePath,
            commandRunner: (_, _) => new StartupTaskService.CommandResult(0, $"""
                Folder: \
                TaskName: AutoPowerRunner
                Task To Run: "{executablePath}"
                Status: Ready
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
    public void IsEnabled_WithSynchronizationContext_DoesNotDeadlock()
    {
        Exception? threadException = null;
        var completed = false;
        var taskName = $"AutoPowerRunner.UnitTest.{Guid.NewGuid():N}";
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
            try
            {
                var service = new StartupTaskService(
                    @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
                    taskName: taskName);

                Assert.False(service.IsEnabled());
                completed = true;
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        })
        {
            IsBackground = true
        };

        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "IsEnabled did not return while a synchronization context was installed.");
        if (threadException is not null)
        {
            throw threadException;
        }

        Assert.True(completed);
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

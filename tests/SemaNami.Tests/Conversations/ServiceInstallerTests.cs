using Moq;
using SemaNami.Core.Conversations;
using Xunit;

namespace SemaNami.Tests.Conversations;

public class ServiceInstallerTests
{
    private const string ExePath = @"C:\Users\me\AppData\Local\Programs\SemaNami\SemaNami.exe";

    [Fact]
    public void WindowsTaskSchedulerRegistrar_Install_RegistersOnLogonTaskAndStartsItImmediately()
    {
        var runner = new Mock<IProcessRunner>();
        var registrar = new WindowsTaskSchedulerRegistrar(runner.Object);

        registrar.Install(ExePath);

        runner.Verify(r => r.Run("schtasks", It.Is<string>(a =>
            a.Contains("/Create") &&
            a.Contains("/TN \"SemaNami Listener\"") &&
            a.Contains(ExePath) &&
            a.Contains("--listen") &&
            a.Contains("/SC ONLOGON") &&
            a.Contains("/RL LIMITED") &&
            a.Contains("/F"))), Times.Once);
        // Started via the task itself (schtasks /Run), not StartDetached — so it launches at the
        // task's own /RL LIMITED integrity level rather than inheriting this (likely elevated)
        // process's token, which would produce a named pipe normal clients can't connect to.
        runner.Verify(r => r.Run("schtasks", It.Is<string>(a =>
            a.Contains("/Run") && a.Contains("/TN \"SemaNami Listener\""))), Times.Once);
        runner.Verify(r => r.StartDetached(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void WindowsTaskSchedulerRegistrar_Install_DelaysStartAfterLogonToAvoidBootRace()
    {
        // Regression test: firing the listener the instant ONLOGON triggers (no delay) races
        // Windows' own session/network bring-up at boot — observed in the wild as the process
        // being killed by the OS moments after launch (exit code 0x40010004, no exception ever
        // logged) and, on the runs that do survive, immediate DNS failures polling Telegram
        // because the network isn't up yet. schtasks' /DELAY is only valid for ONSTART/ONLOGON/
        // ONEVENT triggers, so this only works because the trigger is ONLOGON.
        var runner = new Mock<IProcessRunner>();
        var registrar = new WindowsTaskSchedulerRegistrar(runner.Object);

        registrar.Install(ExePath);

        runner.Verify(r => r.Run("schtasks", It.Is<string>(a =>
            a.Contains("/Create") && a.Contains("/DELAY"))), Times.Once);
    }

    [Fact]
    public void WindowsTaskSchedulerRegistrar_Install_SchtasksCreateFails_ThrowsAndNeverStartsTheListener()
    {
        // Regression test: schtasks /Create's exit code was being silently ignored, so a failed
        // registration still reported success and still started a detached listener — giving
        // false confidence that the persistent, reboot-surviving registration actually happened.
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.Run("schtasks", It.Is<string>(a => a.Contains("/Create")))).Returns(1);
        var registrar = new WindowsTaskSchedulerRegistrar(runner.Object);

        Assert.Throws<InvalidOperationException>(() => registrar.Install(ExePath));

        runner.Verify(r => r.StartDetached(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void WindowsTaskSchedulerRegistrar_Uninstall_DeletesTheTask()
    {
        var runner = new Mock<IProcessRunner>();
        var registrar = new WindowsTaskSchedulerRegistrar(runner.Object);

        registrar.Uninstall();

        runner.Verify(r => r.Run("schtasks", It.Is<string>(a =>
            a.Contains("/Delete") && a.Contains("/TN \"SemaNami Listener\"") && a.Contains("/F"))), Times.Once);
    }

    [Fact]
    public void MacLaunchdRegistrar_Install_WritesPlistAndLoadsIt()
    {
        var plistPath = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".plist");
        try
        {
            var runner = new Mock<IProcessRunner>();
            var registrar = new MacLaunchdRegistrar(runner.Object, plistPath);

            registrar.Install(ExePath);

            Assert.True(File.Exists(plistPath));
            var contents = File.ReadAllText(plistPath);
            Assert.Contains(ExePath, contents);
            Assert.Contains("--listen", contents);
            Assert.Contains("RunAtLoad", contents);
            Assert.Contains("KeepAlive", contents);
            runner.Verify(r => r.Run("launchctl", It.Is<string>(a => a.Contains("unload") && a.Contains(plistPath))), Times.Once);
            runner.Verify(r => r.Run("launchctl", It.Is<string>(a => a.Contains("load") && a.Contains("-w") && a.Contains(plistPath))), Times.Once);
        }
        finally
        {
            if (File.Exists(plistPath)) File.Delete(plistPath);
        }
    }

    [Fact]
    public void MacLaunchdRegistrar_Uninstall_UnloadsAndDeletesThePlist()
    {
        var plistPath = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".plist");
        File.WriteAllText(plistPath, "placeholder");
        var runner = new Mock<IProcessRunner>();
        var registrar = new MacLaunchdRegistrar(runner.Object, plistPath);

        registrar.Uninstall();

        runner.Verify(r => r.Run("launchctl", It.Is<string>(a => a.Contains("unload") && a.Contains(plistPath))), Times.Once);
        Assert.False(File.Exists(plistPath));
    }

    [Fact]
    public void LinuxSystemdRegistrar_SystemdAvailable_WritesUnitFileEnablesAndLingers()
    {
        var unitFilePath = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".service");
        try
        {
            var runner = new Mock<IProcessRunner>();
            runner.Setup(r => r.Run("systemctl", It.IsAny<string>())).Returns(0);
            var registrar = new LinuxSystemdRegistrar(runner.Object, unitFilePath);

            registrar.Install(ExePath);

            Assert.True(File.Exists(unitFilePath));
            var contents = File.ReadAllText(unitFilePath);
            Assert.Contains(ExePath, contents);
            Assert.Contains("--listen", contents);
            Assert.Contains("Restart=on-failure", contents);
            runner.Verify(r => r.Run("systemctl", "--user daemon-reload"), Times.AtLeastOnce);
            runner.Verify(r => r.Run("systemctl", It.Is<string>(a => a.Contains("enable") && a.Contains("--now"))), Times.Once);
            runner.Verify(r => r.Run("loginctl", It.Is<string>(a => a.Contains("enable-linger"))), Times.Once);
        }
        finally
        {
            if (File.Exists(unitFilePath)) File.Delete(unitFilePath);
        }
    }

    [Fact]
    public void LinuxSystemdRegistrar_SystemdUnavailable_FallsBackToCron()
    {
        var unitFilePath = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".service");
        var runner = new Mock<IProcessRunner>();
        // Simulate "systemctl --user status" (the probe) failing — no working user instance.
        runner.Setup(r => r.Run("systemctl", It.IsAny<string>())).Returns(1);
        var registrar = new LinuxSystemdRegistrar(runner.Object, unitFilePath);

        registrar.Install(ExePath);

        Assert.False(File.Exists(unitFilePath));
        runner.Verify(r => r.Run("/bin/sh", It.Is<string>(a => a.Contains("crontab") && a.Contains("@reboot") && a.Contains(ExePath))), Times.Once);
    }

    [Fact]
    public void LinuxSystemdRegistrar_Uninstall_DisablesAndRemovesTheUnitFile()
    {
        var unitFilePath = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid() + ".service");
        File.WriteAllText(unitFilePath, "placeholder");
        var runner = new Mock<IProcessRunner>();
        var registrar = new LinuxSystemdRegistrar(runner.Object, unitFilePath);

        registrar.Uninstall();

        runner.Verify(r => r.Run("systemctl", It.Is<string>(a => a.Contains("disable") && a.Contains("--now"))), Times.Once);
        Assert.False(File.Exists(unitFilePath));
    }
}

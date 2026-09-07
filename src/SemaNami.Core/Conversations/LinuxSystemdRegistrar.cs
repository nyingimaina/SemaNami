namespace SemaNami.Core.Conversations;

public sealed class LinuxSystemdRegistrar : IServiceRegistrar
{
    private const string ServiceName = "semanami-listen.service";

    private readonly IProcessRunner processRunner;
    private readonly string unitFilePath;

    public LinuxSystemdRegistrar(IProcessRunner processRunner)
        : this(processRunner, DefaultUnitFilePath())
    {
    }

    // Internal, explicit-path constructor for unit tests — avoids touching the real
    // ~/.config/systemd/user.
    internal LinuxSystemdRegistrar(IProcessRunner processRunner, string unitFilePath)
    {
        this.processRunner = processRunner;
        this.unitFilePath = unitFilePath;
    }

    private static string DefaultUnitFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user", ServiceName);

    public void Install(string exePath)
    {
        if (!SystemdUserAvailable())
        {
            InstallCronFallback(exePath);
            return;
        }

        var directory = Path.GetDirectoryName(unitFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(unitFilePath, BuildUnitFile(exePath));

        processRunner.Run("systemctl", "--user daemon-reload");
        processRunner.Run("systemctl", $"--user enable --now {ServiceName}");

        // Without this, systemd tears down the user's --user instance (and this service) as
        // soon as their last login session ends — silently defeating the whole point.
        processRunner.Run("loginctl", $"enable-linger {Environment.UserName}");
    }

    public void Uninstall()
    {
        processRunner.Run("systemctl", $"--user disable --now {ServiceName}");
        if (File.Exists(unitFilePath))
        {
            File.Delete(unitFilePath);
        }
        processRunner.Run("systemctl", "--user daemon-reload");
        // Deliberately does not touch linger — other user services on the machine may depend on it.
    }

    private bool SystemdUserAvailable() => processRunner.Run("systemctl", "--user status") == 0;

    private void InstallCronFallback(string exePath)
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".semanami", "listen.log");
        var cronLine = $"@reboot {exePath} --listen >> \"{logPath}\" 2>&1";
        // Weaker than the systemd path by design: no crash-restart — a crashed listener stays
        // dead until next reboot. Documented as the accepted MVP fallback, not fixed here.
        processRunner.Run("/bin/sh", $"-c \"(crontab -l 2>/dev/null | grep -v 'SemaNami --listen'; echo '{cronLine}') | crontab -\"");
    }

    private static string BuildUnitFile(string exePath) => $"""
        [Unit]
        Description=SemaNami Telegram listener

        [Service]
        ExecStart={exePath} --listen
        Restart=on-failure
        RestartSec=5

        [Install]
        WantedBy=default.target
        """;
}

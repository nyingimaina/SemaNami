namespace SemaNami.Core.Conversations;

public sealed class MacLaunchdRegistrar : IServiceRegistrar
{
    private const string Label = "com.nyingi.semanami.listen";

    private readonly IProcessRunner processRunner;
    private readonly string plistPath;

    public MacLaunchdRegistrar(IProcessRunner processRunner)
        : this(processRunner, DefaultPlistPath())
    {
    }

    // Internal, explicit-path constructor for unit tests — avoids touching the real
    // ~/Library/LaunchAgents.
    internal MacLaunchdRegistrar(IProcessRunner processRunner, string plistPath)
    {
        this.processRunner = processRunner;
        this.plistPath = plistPath;
    }

    private static string DefaultPlistPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", $"{Label}.plist");

    public void Install(string exePath)
    {
        var directory = Path.GetDirectoryName(plistPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".semanami", "listen.log");
        File.WriteAllText(plistPath, BuildPlist(exePath, logPath));

        // Unload-then-load makes registration idempotent; unload's exit code is deliberately
        // ignored (it fails harmlessly if nothing was loaded yet, e.g. on first install).
        processRunner.Run("launchctl", $"unload \"{plistPath}\"");
        processRunner.Run("launchctl", $"load -w \"{plistPath}\"");
    }

    public void Uninstall()
    {
        // KeepAlive=true relaunches the process even after a clean exit — going through
        // launchctl unload (never just killing the process) is required, not optional.
        processRunner.Run("launchctl", $"unload \"{plistPath}\"");
        if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }
    }

    private static string BuildPlist(string exePath, string logPath) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>{Label}</string>
            <key>ProgramArguments</key>
            <array>
                <string>{exePath}</string>
                <string>--listen</string>
            </array>
            <key>RunAtLoad</key>
            <true/>
            <key>KeepAlive</key>
            <true/>
            <key>StandardOutPath</key>
            <string>{logPath}</string>
            <key>StandardErrorPath</key>
            <string>{logPath}</string>
        </dict>
        </plist>
        """;
}

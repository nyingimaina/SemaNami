namespace SemaNami.Core.Conversations;

// Task Scheduler, not a real Windows Service (services register/start differently and add no
// benefit here). The registered task itself runs with a normal, non-admin token (/RL LIMITED)
// at every logon; the installer runs elevated only because *creating* an ONLOGON trigger can
// itself require an elevated caller (see the exit-code check in Install below).
public sealed class WindowsTaskSchedulerRegistrar : IServiceRegistrar
{
    private const string TaskName = "SemaNami Listener";

    private readonly IProcessRunner processRunner;

    public WindowsTaskSchedulerRegistrar(IProcessRunner processRunner)
    {
        this.processRunner = processRunner;
    }

    public void Install(string exePath)
    {
        var taskCommand = $"\"{exePath}\" --listen";
        // /RL LIMITED = the registered task itself runs with the current user's normal token at
        // logon (no admin needed to RUN it) — but *creating* an ONLOGON trigger can itself
        // require an elevated/interactive caller (observed: fails with "Access is denied" from a
        // non-interactive automation shell even with /RL LIMITED). The installer runs this
        // elevated for exactly that reason. /F makes re-registration idempotent.
        // /DELAY 0:30 — firing the instant the ONLOGON trigger does races Windows' own session
        // and network bring-up at boot (observed in the wild: the process killed by the OS
        // moments after launch with no exception ever logged, and on runs that do survive,
        // immediate DNS failures polling Telegram because the network isn't up yet). /DELAY is
        // only valid for ONSTART/ONLOGON/ONEVENT triggers, which ONLOGON is.
        var exitCode = processRunner.Run("schtasks", $"/Create /TN \"{TaskName}\" /TR \"{taskCommand}\" /SC ONLOGON /DELAY 0:30 /RL LIMITED /F");
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"schtasks /Create failed (exit code {exitCode}). Try running this from an elevated (Administrator) terminal.");
        }

        // Start it now too, so the user doesn't have to log out/in for the listener to be live —
        // via the task itself (not StartDetached), so it launches with the task's own /RL
        // LIMITED integrity level. Launching it directly here would inherit whatever privilege
        // level *this* process has (typically elevated, since creating the task above requires
        // it), producing a High-integrity-level named pipe that a normal client can't connect to.
        processRunner.Run("schtasks", $"/Run /TN \"{TaskName}\"");
    }

    public void Uninstall()
    {
        processRunner.Run("schtasks", $"/Delete /TN \"{TaskName}\" /F");
    }
}

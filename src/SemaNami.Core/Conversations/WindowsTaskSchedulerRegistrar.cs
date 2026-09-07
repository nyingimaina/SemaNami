namespace SemaNami.Core.Conversations;

// Task Scheduler, not a real Windows Service — a service needs admin, which would contradict
// the installer's existing per-user, no-admin design (PrivilegesRequired=lowest).
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
        // /RL LIMITED = current user's normal token, no UAC. /F makes re-registration idempotent.
        processRunner.Run("schtasks", $"/Create /TN \"{TaskName}\" /TR \"{taskCommand}\" /SC ONLOGON /RL LIMITED /F");

        // Start it now too, so the user doesn't have to log out/in for the listener to be live.
        processRunner.StartDetached(exePath, "--listen");
    }

    public void Uninstall()
    {
        processRunner.Run("schtasks", $"/Delete /TN \"{TaskName}\" /F");
    }
}

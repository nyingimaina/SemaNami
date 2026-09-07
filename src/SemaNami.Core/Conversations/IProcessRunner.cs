namespace SemaNami.Core.Conversations;

public interface IProcessRunner
{
    // Runs a process and waits for it to exit, returning its exit code — for short-lived
    // registration commands (schtasks, launchctl, systemctl).
    int Run(string fileName, string arguments);

    // Starts a process without waiting — for launching the long-running --listen process itself
    // immediately after registration, so the user doesn't have to log out/in first.
    void StartDetached(string fileName, string arguments);
}

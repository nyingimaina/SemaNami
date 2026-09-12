using System.Runtime.InteropServices;

namespace SemaNami.Cli;

// --listen is meant to run invisibly in the background (a scheduled-task-launched listener), but
// a console-subsystem exe still gets a window from Windows whenever a fresh console is created
// for it — which is exactly what happens when Task Scheduler launches it with no parent shell.
// Hides that window, but only when this process is the console's sole owner: a --listen started
// manually from an already-open terminal shares that terminal's console, and hiding it would
// hide the user's whole terminal window, not just this process.
public static class ConsoleWindowHider
{
    private const int SW_HIDE = 0;

    public static bool ShouldHide(int consoleProcessCount) => consoleProcessCount == 1;

    public static void HideIfOwnedSolelyByThisProcess()
    {
        var consoleWindow = GetConsoleWindow();
        if (consoleWindow == IntPtr.Zero)
        {
            return;
        }

        var processList = new uint[4];
        var consoleProcessCount = GetConsoleProcessList(processList, processList.Length);

        if (ShouldHide(consoleProcessCount))
        {
            ShowWindow(consoleWindow, SW_HIDE);
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int GetConsoleProcessList(uint[] processList, int processCount);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}

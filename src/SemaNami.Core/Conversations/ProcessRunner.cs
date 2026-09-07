using System.Diagnostics;

namespace SemaNami.Core.Conversations;

// Thin wrapper over System.Diagnostics.Process — no dedicated unit tests, same convention as
// other real-I/O wrappers in this codebase; the registrar classes that use it are tested via a
// mocked IProcessRunner instead.
public sealed class ProcessRunner : IProcessRunner
{
    public int Run(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        process!.WaitForExit();
        return process.ExitCode;
    }

    public void StartDetached(string fileName, string arguments)
    {
        Process.Start(new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}

using SemaNami.Core;

namespace SemaNami.Cli;

public sealed class ConsoleSetupIO : ISetupIO
{
    public void WriteLine(string text) => Console.WriteLine(text);

    public string? ReadLine() => Console.ReadLine();
}

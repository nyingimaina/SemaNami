namespace SemaNami.Cli;

public abstract record CliCommand
{
    public sealed record Send(string Sender, string Message) : CliCommand;
    public sealed record GetChatId : CliCommand;
    public sealed record RunSetup : CliCommand;
    public sealed record ShowUsage : CliCommand;
    public sealed record Invalid(string Error) : CliCommand;
}

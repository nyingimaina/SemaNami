namespace TelegramNotifier.Cli;

public abstract record CliCommand
{
    public sealed record Send(string Message) : CliCommand;
    public sealed record GetChatId : CliCommand;
    public sealed record Invalid(string Error) : CliCommand;
}

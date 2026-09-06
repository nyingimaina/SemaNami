namespace SemaNami.Cli;

public static class CliArgs
{
    private const string Usage = "Usage: notify \"message text\"   OR   notify --get-chat-id";

    public static CliCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliCommand.Invalid(Usage);
        }

        if (args[0] == "--get-chat-id")
        {
            return new CliCommand.GetChatId();
        }

        var message = string.Join(" ", args);
        if (string.IsNullOrWhiteSpace(message))
        {
            return new CliCommand.Invalid("Message must not be empty.\n" + Usage);
        }

        return new CliCommand.Send(message);
    }
}

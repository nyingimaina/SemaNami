namespace SemaNami.Cli;

public static class CliArgs
{
    public const string Usage =
        "SemaNami - send yourself a Telegram notification from any tool.\n" +
        "\n" +
        "Usage:\n" +
        "  SemaNami -sender \"<Sender Name>\" -message \"<Chat Message>\"\n" +
        "  SemaNami --setup\n" +
        "  SemaNami --get-chat-id\n" +
        "  SemaNami --help\n" +
        "\n" +
        "Example:\n" +
        "  SemaNami -sender \"Build Script\" -message \"Build finished successfully\"\n" +
        "\n" +
        "First time? Run `SemaNami --setup` to configure everything.\n" +
        "Requires environment variables TELEGRAM_BOT_TOKEN and TELEGRAM_CHAT_ID, which\n" +
        "--setup configures for you.";

    public static CliCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliCommand.ShowUsage();
        }

        if (args.Length == 1 && (args[0] == "-h" || args[0] == "--help"))
        {
            return new CliCommand.ShowUsage();
        }

        if (args.Length == 1 && args[0] == "--get-chat-id")
        {
            return new CliCommand.GetChatId();
        }

        if (args.Length == 1 && args[0] == "--setup")
        {
            return new CliCommand.RunSetup();
        }

        string? sender = null;
        string? message = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-sender" when i + 1 < args.Length:
                    sender = args[++i];
                    break;
                case "-message" when i + 1 < args.Length:
                    message = args[++i];
                    break;
                default:
                    return new CliCommand.Invalid($"Unrecognized or incomplete argument: {args[i]}\n\n{Usage}");
            }
        }

        if (string.IsNullOrWhiteSpace(sender))
        {
            return new CliCommand.Invalid($"Missing required argument: -sender\n\n{Usage}");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return new CliCommand.Invalid($"Missing required argument: -message\n\n{Usage}");
        }

        return new CliCommand.Send(sender, message);
    }
}

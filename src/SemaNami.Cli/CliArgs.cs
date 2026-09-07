namespace SemaNami.Cli;

public static class CliArgs
{
    public const int DefaultWaitTimeoutSeconds = 300;

    // Every mode is documented here, not just listed — this is what a bare `SemaNami` prints,
    // a caller's first point of contact, so it needs to make clear which mode fits which
    // situation, not just what flags exist.
    public static readonly string Usage =
        "SemaNami - send yourself a Telegram message from any tool, and get replies back.\n" +
        "\n" +
        "ONE-WAY NOTIFICATION - no reply expected:\n" +
        "  SemaNami -sender \"<Sender Name>\" -message \"<Chat Message>\"\n" +
        "  Example: SemaNami -sender \"Build Script\" -message \"Build finished successfully\"\n" +
        "\n" +
        "MULTI-TURN CONVERSATION, ASYNC - starts or continues a thread; returns immediately;\n" +
        "the reply may come back seconds or days later, from any process:\n" +
        "  SemaNami -sender \"<Sender Name>\" -message \"<Chat Message>\" -conversation <id>\n" +
        "  Example: SemaNami -sender \"Deploy\" -message \"Ship it?\" -conversation deploy-42\n" +
        "\n" +
        "READ A CONVERSATION - check for replies at any later time, from any process:\n" +
        "  SemaNami --conversation-history <id> -sender \"<Sender Name>\" [-after <seq>]\n" +
        "\n" +
        "CLOSE A CONVERSATION - stop it counting toward the open-conversation fallback once\n" +
        "you're done with it:\n" +
        "  SemaNami --close-conversation <id> -sender \"<Sender Name>\"\n" +
        "\n" +
        "REALTIME WAIT - block until the next reply arrives, or time out; use only when you're\n" +
        "actively waiting right now, not for arbitrary-delay scenarios:\n" +
        "  SemaNami --wait-for-reply -sender \"<Sender Name>\" -conversation <id> [-after <seq>] [--timeout <seconds>]\n" +
        $"  (--timeout defaults to {DefaultWaitTimeoutSeconds} seconds)\n" +
        "\n" +
        "SETUP - one-time configuration, and finding your Telegram chat id:\n" +
        "  SemaNami --setup\n" +
        "  SemaNami --get-chat-id\n" +
        "\n" +
        "SERVICE MANAGEMENT - normally run by the installer automatically; only needed by hand\n" +
        "to fix a broken registration:\n" +
        "  SemaNami --install-service\n" +
        "  SemaNami --uninstall-service\n" +
        "\n" +
        "BACKGROUND LISTENER - normally auto-started as a service, not run directly:\n" +
        "  SemaNami --listen\n" +
        "\n" +
        "  SemaNami --help\n" +
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

        if (args.Length == 1 && args[0] == "--listen")
        {
            return new CliCommand.Listen();
        }

        if (args.Length == 1 && args[0] == "--install-service")
        {
            return new CliCommand.InstallService();
        }

        if (args.Length == 1 && args[0] == "--uninstall-service")
        {
            return new CliCommand.UninstallService();
        }

        if (args[0] == "--conversation-history")
        {
            return ParseConversationHistory(args);
        }

        if (args[0] == "--close-conversation")
        {
            return ParseCloseConversation(args);
        }

        if (args[0] == "--wait-for-reply")
        {
            return ParseWaitForReply(args);
        }

        return ParseSend(args);
    }

    private static CliCommand ParseConversationHistory(string[] args)
    {
        if (args.Length < 2)
        {
            return new CliCommand.Invalid($"Missing required argument: conversation id\n\n{Usage}");
        }

        var conversationId = args[1];
        string? sender = null;
        long? after = null;

        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-sender" when i + 1 < args.Length:
                    sender = args[++i];
                    break;
                case "-after" when i + 1 < args.Length:
                    if (!long.TryParse(args[i + 1], out var parsedAfter))
                    {
                        return new CliCommand.Invalid($"Invalid value for -after: {args[i + 1]}\n\n{Usage}");
                    }
                    after = parsedAfter;
                    i++;
                    break;
                default:
                    return new CliCommand.Invalid($"Unrecognized or incomplete argument: {args[i]}\n\n{Usage}");
            }
        }

        if (string.IsNullOrWhiteSpace(sender))
        {
            return new CliCommand.Invalid($"Missing required argument: -sender\n\n{Usage}");
        }

        return new CliCommand.ConversationHistory(sender, conversationId, after);
    }

    private static CliCommand ParseCloseConversation(string[] args)
    {
        if (args.Length < 2)
        {
            return new CliCommand.Invalid($"Missing required argument: conversation id\n\n{Usage}");
        }

        var conversationId = args[1];
        string? sender = null;

        for (var i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-sender" when i + 1 < args.Length:
                    sender = args[++i];
                    break;
                default:
                    return new CliCommand.Invalid($"Unrecognized or incomplete argument: {args[i]}\n\n{Usage}");
            }
        }

        if (string.IsNullOrWhiteSpace(sender))
        {
            return new CliCommand.Invalid($"Missing required argument: -sender\n\n{Usage}");
        }

        return new CliCommand.CloseConversation(sender, conversationId);
    }

    private static CliCommand ParseWaitForReply(string[] args)
    {
        string? sender = null;
        string? conversationId = null;
        long? after = null;
        var timeoutSeconds = DefaultWaitTimeoutSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-sender" when i + 1 < args.Length:
                    sender = args[++i];
                    break;
                case "-conversation" when i + 1 < args.Length:
                    conversationId = args[++i];
                    break;
                case "-after" when i + 1 < args.Length:
                    if (!long.TryParse(args[i + 1], out var parsedAfter))
                    {
                        return new CliCommand.Invalid($"Invalid value for -after: {args[i + 1]}\n\n{Usage}");
                    }
                    after = parsedAfter;
                    i++;
                    break;
                case "--timeout" when i + 1 < args.Length:
                    if (!int.TryParse(args[i + 1], out var parsedTimeout))
                    {
                        return new CliCommand.Invalid($"Invalid value for --timeout: {args[i + 1]}\n\n{Usage}");
                    }
                    timeoutSeconds = parsedTimeout;
                    i++;
                    break;
                default:
                    return new CliCommand.Invalid($"Unrecognized or incomplete argument: {args[i]}\n\n{Usage}");
            }
        }

        if (string.IsNullOrWhiteSpace(sender))
        {
            return new CliCommand.Invalid($"Missing required argument: -sender\n\n{Usage}");
        }

        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return new CliCommand.Invalid($"Missing required argument: -conversation\n\n{Usage}");
        }

        return new CliCommand.WaitForReply(sender, conversationId, after, timeoutSeconds);
    }

    private static CliCommand ParseSend(string[] args)
    {
        string? sender = null;
        string? message = null;
        string? conversationId = null;

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
                case "-conversation" when i + 1 < args.Length:
                    conversationId = args[++i];
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

        return new CliCommand.Send(sender, message, conversationId);
    }
}

namespace SemaNami.Core;

public static class MessageFormatter
{
    // Plain text, deliberately no Markdown/HTML — arbitrary sender/message content (from any
    // tool, any characters) must never risk breaking Telegram's formatting parser.
    public static string WithSender(string sender, string message) => $"{sender}: {message}";
}

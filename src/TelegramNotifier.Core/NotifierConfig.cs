namespace TelegramNotifier.Core;

public sealed record NotifierConfig(string BotToken, string ChatId)
{
    // getEnvironmentVariable is injected (rather than calling Environment.GetEnvironmentVariable
    // directly) purely so this is unit-testable without mutating real process environment state.
    public static NotifierConfig? FromEnvironment(Func<string, string?> getEnvironmentVariable)
    {
        var botToken = getEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var chatId = getEnvironmentVariable("TELEGRAM_CHAT_ID");

        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return null;
        }

        return new NotifierConfig(botToken, chatId);
    }
}

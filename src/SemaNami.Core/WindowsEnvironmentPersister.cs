namespace SemaNami.Core;

// Persists to the current Windows user's environment block (no admin/UAC needed). New
// processes started after this runs (e.g. a freshly opened terminal) inherit it automatically.
public sealed class WindowsEnvironmentPersister : IEnvironmentPersister
{
    private const string BotTokenVar = "TELEGRAM_BOT_TOKEN";
    private const string ChatIdVar = "TELEGRAM_CHAT_ID";

    public void Persist(string botToken, string chatId)
    {
        Environment.SetEnvironmentVariable(BotTokenVar, botToken, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable(ChatIdVar, chatId, EnvironmentVariableTarget.User);
    }

    public (string? BotToken, string? ChatId) ReadExisting()
    {
        var token = Environment.GetEnvironmentVariable(BotTokenVar, EnvironmentVariableTarget.User);
        var chatId = Environment.GetEnvironmentVariable(ChatIdVar, EnvironmentVariableTarget.User);
        return (
            string.IsNullOrWhiteSpace(token) ? null : token,
            string.IsNullOrWhiteSpace(chatId) ? null : chatId);
    }
}

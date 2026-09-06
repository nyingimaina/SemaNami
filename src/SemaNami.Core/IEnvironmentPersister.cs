namespace SemaNami.Core;

// Persists the bot token/chat id so future processes (any tool invoking SemaNami) can find
// them without reconfiguring. Implementations are platform-specific (Windows user environment
// variable vs. a shell profile file on macOS/Linux).
public interface IEnvironmentPersister
{
    void Persist(string botToken, string chatId);

    (string? BotToken, string? ChatId) ReadExisting();
}

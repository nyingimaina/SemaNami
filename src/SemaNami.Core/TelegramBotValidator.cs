using Telegram.Bot;

namespace SemaNami.Core;

// Thin wrapper over Telegram.Bot's getMe call — kept free of branching logic so it doesn't
// need its own unit tests; SetupWizard's tests cover this interface's contract via a mock.
public sealed class TelegramBotValidator : IBotValidator
{
    public async Task<string?> TryGetBotUsernameAsync(string botToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = new TelegramBotClient(botToken);
            var me = await client.GetMe(cancellationToken);
            return me.Username;
        }
        catch
        {
            return null;
        }
    }
}

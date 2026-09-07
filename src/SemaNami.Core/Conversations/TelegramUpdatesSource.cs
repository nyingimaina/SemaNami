using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace SemaNami.Core.Conversations;

// Thin wrapper over Telegram.Bot's getUpdates — kept free of branching logic so it doesn't need
// its own unit tests; ConversationListener's tests cover the contract via a mock.
public sealed class TelegramUpdatesSource : IUpdatesSource
{
    private readonly ITelegramBotClient client;
    private readonly long chatId;

    public TelegramUpdatesSource(string botToken, long chatId)
    {
        client = new TelegramBotClient(botToken);
        this.chatId = chatId;
    }

    public async Task<IReadOnlyList<IncomingUpdate>> GetUpdatesAsync(int? offset, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var updates = await client.GetUpdates(
            offset: offset,
            timeout: timeoutSeconds,
            allowedUpdates: new[] { UpdateType.Message },
            cancellationToken: cancellationToken);

        return updates
            .Where(u => u.Message is not null && u.Message.Chat.Id == chatId && u.Message.Text is not null)
            .Select(u => new IncomingUpdate(
                u.Id,
                u.Message!.Chat.Id,
                u.Message.MessageId,
                u.Message.ReplyToMessage?.MessageId,
                u.Message.Text!))
            .ToList();
    }
}

using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramNotifier.Core;

public interface IChatIdLookup
{
    Task<IReadOnlyList<long>> GetRecentChatIdsAsync(CancellationToken cancellationToken = default);
}

// One-time setup helper: after you message your bot directly in Telegram, this reads that
// message back via getUpdates so you can learn your own chat id without hand-crafting a curl call.
public sealed class TelegramChatIdLookup : IChatIdLookup
{
    private readonly ITelegramBotClient client;

    public TelegramChatIdLookup(string botToken)
    {
        client = new TelegramBotClient(botToken);
    }

    public async Task<IReadOnlyList<long>> GetRecentChatIdsAsync(CancellationToken cancellationToken = default)
    {
        var updates = await client.GetUpdates(cancellationToken: cancellationToken);
        return ExtractDistinctChatIds(updates);
    }

    public static IReadOnlyList<long> ExtractDistinctChatIds(IEnumerable<Update> updates) =>
        updates
            .Select(u => u.Message?.Chat.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
}

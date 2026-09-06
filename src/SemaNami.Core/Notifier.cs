namespace SemaNami.Core;

// Facade any tool references directly: construct once with a message sender and destination
// chat id, then call NotifyAsync for every notification.
public sealed class Notifier
{
    private readonly ITelegramMessageSender sender;
    private readonly string chatId;

    public Notifier(ITelegramMessageSender sender, string chatId)
    {
        ArgumentNullException.ThrowIfNull(sender);
        if (string.IsNullOrWhiteSpace(chatId))
        {
            throw new ArgumentException("Chat id must not be empty.", nameof(chatId));
        }

        this.sender = sender;
        this.chatId = chatId;
    }

    public async Task NotifyAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        await sender.SendMessageAsync(chatId, message, cancellationToken);
    }
}

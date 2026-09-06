namespace TelegramNotifier.Core;

public interface ITelegramMessageSender
{
    Task SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default);
}

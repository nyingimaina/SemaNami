using SemaNami.Cli;
using SemaNami.Core;

var command = CliArgs.Parse(args);

return command switch
{
    CliCommand.Invalid invalid => Fail(invalid.Error),
    CliCommand.GetChatId => await RunGetChatIdAsync(),
    CliCommand.Send send => await RunSendAsync(send.Message),
    _ => Fail("Unrecognized command."),
};

int Fail(string error)
{
    Console.Error.WriteLine(error);
    return 1;
}

async Task<int> RunGetChatIdAsync()
{
    var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
    if (string.IsNullOrWhiteSpace(botToken))
    {
        return Fail("TELEGRAM_BOT_TOKEN is not set. Create a bot via @BotFather first, then set that environment variable.");
    }

    var lookup = new TelegramChatIdLookup(botToken);
    var chatIds = await lookup.GetRecentChatIdsAsync();

    if (chatIds.Count == 0)
    {
        return Fail("No messages found yet. Open a chat with your bot in Telegram, send it any message, then run this again.");
    }

    foreach (var chatId in chatIds)
    {
        Console.WriteLine(chatId);
    }

    return 0;
}

async Task<int> RunSendAsync(string message)
{
    var config = NotifierConfig.FromEnvironment(Environment.GetEnvironmentVariable);
    if (config is null)
    {
        return Fail("Set TELEGRAM_BOT_TOKEN and TELEGRAM_CHAT_ID environment variables first (see README.md).");
    }

    var sender = new TelegramBotMessageSender(config.BotToken);
    var notifier = new Notifier(sender, config.ChatId);

    try
    {
        await notifier.NotifyAsync(message);
        Console.WriteLine("Notification sent.");
        return 0;
    }
    catch (Exception ex)
    {
        return Fail($"Failed to send notification: {ex.Message}");
    }
}

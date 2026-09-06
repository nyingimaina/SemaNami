using SemaNami.Cli;
using SemaNami.Core;

var command = CliArgs.Parse(args);

return command switch
{
    CliCommand.ShowUsage => ShowUsage(),
    CliCommand.Invalid invalid => Fail(invalid.Error),
    CliCommand.GetChatId => await RunGetChatIdAsync(),
    CliCommand.RunSetup => await RunSetupAsync(),
    CliCommand.Send send => await RunSendAsync(send.Sender, send.Message),
    _ => Fail("Unrecognized command."),
};

int ShowUsage()
{
    Console.WriteLine(CliArgs.Usage);
    return 0;
}

int Fail(string error)
{
    Console.Error.WriteLine(error);
    return 1;
}

IEnvironmentPersister CreatePersister() =>
    OperatingSystem.IsWindows()
        ? new WindowsEnvironmentPersister()
        : new UnixEnvironmentPersister();

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

async Task<int> RunSetupAsync()
{
    var wizard = new SetupWizard(
        new ConsoleSetupIO(),
        CreatePersister(),
        token => new TelegramBotValidator(),
        token => new TelegramChatIdLookup(token),
        token => new TelegramBotMessageSender(token));

    var succeeded = await wizard.RunAsync();
    return succeeded ? 0 : 1;
}

async Task<int> RunSendAsync(string sender, string message)
{
    var persister = CreatePersister();
    var existing = persister.ReadExisting();
    if (existing.BotToken is null || existing.ChatId is null)
    {
        return Fail("SemaNami is not configured yet. Run `SemaNami --setup` first.");
    }

    var config = new NotifierConfig(existing.BotToken, existing.ChatId);
    var messageSender = new TelegramBotMessageSender(config.BotToken);
    var notifier = new Notifier(messageSender, config.ChatId);

    try
    {
        await notifier.NotifyAsync(MessageFormatter.WithSender(sender, message));
        Console.WriteLine("Notification sent.");
        return 0;
    }
    catch (Exception ex)
    {
        return Fail($"Failed to send notification: {ex.Message}");
    }
}

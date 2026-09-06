namespace SemaNami.Core;

// Drives first-run configuration end to end. Everything that CAN be automated is: validating
// the token, detecting the chat id, persisting both, and sending a live confirmation message.
// The one step that fundamentally can't be automated is creating the bot itself — Telegram
// requires a human to talk to @BotFather; there's no API for one app to create a bot on
// another's behalf.
public sealed class SetupWizard
{
    private const int MaxChatIdLookupAttempts = 3;

    private readonly ISetupIO io;
    private readonly IEnvironmentPersister persister;
    private readonly Func<string, IBotValidator> botValidatorFactory;
    private readonly Func<string, IChatIdLookup> chatIdLookupFactory;
    private readonly Func<string, ITelegramMessageSender> senderFactory;

    public SetupWizard(
        ISetupIO io,
        IEnvironmentPersister persister,
        Func<string, IBotValidator> botValidatorFactory,
        Func<string, IChatIdLookup> chatIdLookupFactory,
        Func<string, ITelegramMessageSender> senderFactory)
    {
        this.io = io;
        this.persister = persister;
        this.botValidatorFactory = botValidatorFactory;
        this.chatIdLookupFactory = chatIdLookupFactory;
        this.senderFactory = senderFactory;
    }

    public async Task<bool> RunAsync(CancellationToken cancellationToken = default)
    {
        var existing = persister.ReadExisting();
        if (existing.BotToken is not null && existing.ChatId is not null)
        {
            io.WriteLine("SemaNami is already configured.");
            return true;
        }

        io.WriteLine("Let's get SemaNami set up.");
        io.WriteLine(string.Empty);
        io.WriteLine("1. Open Telegram and search for @BotFather (or visit https://t.me/BotFather).");
        io.WriteLine("2. Send /newbot and follow the prompts.");
        io.WriteLine("3. Copy the token BotFather gives you and paste it below.");
        io.WriteLine(string.Empty);
        io.WriteLine("Bot token:");
        var token = io.ReadLine();

        if (string.IsNullOrWhiteSpace(token))
        {
            io.WriteLine("No token entered. Run `SemaNami --setup` again when you're ready.");
            return false;
        }

        var validator = botValidatorFactory(token);
        var username = await validator.TryGetBotUsernameAsync(token, cancellationToken);
        if (username is null)
        {
            io.WriteLine("That doesn't look like a valid bot token. Run `SemaNami --setup` again to retry.");
            return false;
        }

        io.WriteLine($"Found your bot: @{username}");
        io.WriteLine($"Now open Telegram, message @{username} (anything, e.g. \"hi\"), then press Enter here.");
        io.ReadLine();

        var lookup = chatIdLookupFactory(token);
        var chatIds = Array.Empty<long>() as IReadOnlyList<long>;
        for (var attempt = 0; attempt < MaxChatIdLookupAttempts && chatIds.Count == 0; attempt++)
        {
            chatIds = await lookup.GetRecentChatIdsAsync(cancellationToken);
            if (chatIds.Count == 0 && attempt < MaxChatIdLookupAttempts - 1)
            {
                io.WriteLine("Didn't find a message yet — make sure you sent it, then press Enter to check again.");
                io.ReadLine();
            }
        }

        if (chatIds.Count == 0)
        {
            io.WriteLine("Still no message found. Run `SemaNami --setup` again once you've messaged the bot.");
            return false;
        }

        var chatId = chatIds[0].ToString();
        persister.Persist(token, chatId);
        io.WriteLine("Configuration saved.");

        var sender = senderFactory(token);
        await sender.SendMessageAsync(chatId, "SemaNami is set up! You'll get your notifications here.", cancellationToken);
        io.WriteLine("Sent a confirmation message — check Telegram.");
        io.WriteLine("Open a new terminal window for other tools to pick up the configuration.");
        return true;
    }
}

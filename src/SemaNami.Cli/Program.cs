using System.IO.Pipes;
using System.Text.Json;
using SemaNami.Cli;
using SemaNami.Core;
using SemaNami.Core.Conversations;

var command = CliArgs.Parse(args);

return command switch
{
    CliCommand.ShowUsage => ShowUsage(),
    CliCommand.Invalid invalid => Fail(invalid.Error),
    CliCommand.GetChatId => await RunGetChatIdAsync(),
    CliCommand.RunSetup => await RunSetupAsync(),
    CliCommand.Send send => await RunSendAsync(send.Sender, send.Message, send.ConversationId),
    CliCommand.ConversationHistory history => RunConversationHistory(history.Sender, history.ConversationId, history.After),
    CliCommand.CloseConversation close => RunCloseConversation(close.Sender, close.ConversationId),
    CliCommand.Listen => await RunListenAsync(),
    CliCommand.InstallService => RunInstallService(),
    CliCommand.UninstallService => RunUninstallService(),
    CliCommand.WaitForReply wait => await RunWaitForReplyAsync(wait.Sender, wait.ConversationId, wait.After, wait.TimeoutSeconds),
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

IServiceRegistrar CreateServiceRegistrar()
{
    var processRunner = new ProcessRunner();
    if (OperatingSystem.IsWindows()) return new WindowsTaskSchedulerRegistrar(processRunner);
    if (OperatingSystem.IsMacOS()) return new MacLaunchdRegistrar(processRunner);
    return new LinuxSystemdRegistrar(processRunner);
}

string GetDbPath() =>
    OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SemaNami", "semanami.db")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".semanami", "semanami.db");

(string BotToken, string ChatId)? GetConfig()
{
    var existing = CreatePersister().ReadExisting();
    return existing.BotToken is not null && existing.ChatId is not null
        ? (existing.BotToken, existing.ChatId)
        : null;
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

async Task<int> RunSendAsync(string sender, string message, string? conversationId)
{
    var config = GetConfig();
    if (config is null)
    {
        return Fail("SemaNami is not configured yet. Run `SemaNami --setup` first.");
    }

    var formatted = MessageFormatter.WithSender(sender, message);
    var messageSender = new TelegramBotMessageSender(config.Value.BotToken);

    try
    {
        if (conversationId is null)
        {
            var notifier = new Notifier(messageSender, config.Value.ChatId);
            await notifier.NotifyAsync(formatted);
        }
        else
        {
            var store = new SqliteConversationStore(GetDbPath());
            var conversationSender = new ConversationSender(store, messageSender, config.Value.ChatId);
            await conversationSender.SendAsync(sender, conversationId, formatted);
        }

        Console.WriteLine("Notification sent.");
        return 0;
    }
    catch (Exception ex)
    {
        return Fail($"Failed to send notification: {ex.Message}");
    }
}

int RunConversationHistory(string sender, string conversationId, long? after)
{
    var store = new SqliteConversationStore(GetDbPath());
    var history = store.GetHistory(sender, conversationId, after);
    Console.WriteLine(JsonSerializer.Serialize(history));
    return 0;
}

int RunCloseConversation(string sender, string conversationId)
{
    var store = new SqliteConversationStore(GetDbPath());
    store.CloseConversation(sender, conversationId);
    Console.WriteLine("Conversation closed.");
    return 0;
}

int RunInstallService()
{
    var exePath = Environment.ProcessPath;
    if (exePath is null)
    {
        return Fail("Could not determine the running executable's path.");
    }

    try
    {
        CreateServiceRegistrar().Install(exePath);
    }
    catch (Exception ex)
    {
        return Fail($"Failed to install the SemaNami listener service: {ex.Message}");
    }

    Console.WriteLine("SemaNami listener service installed and started.");
    return 0;
}

int RunUninstallService()
{
    CreateServiceRegistrar().Uninstall();
    Console.WriteLine("SemaNami listener service uninstalled.");
    return 0;
}

string GetLogPath() => Path.Combine(Path.GetDirectoryName(GetDbPath())!, "listen.log");

void LogToFile(string message)
{
    try
    {
        var logPath = GetLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.AppendAllText(logPath, $"{DateTime.UtcNow:O} {message}\n");
    }
    catch
    {
        // Logging is best-effort — never let a logging failure mask the real error.
    }
}

async Task<int> RunListenAsync()
{
    // A launch context with no attached console (observed: Task Scheduler) can make even basic
    // Console I/O throw here. Wrapping the whole method means a failure this early — or any
    // other unexpected exception — is captured to listen.log instead of the process silently
    // vanishing with nothing to diagnose it by.
    try
    {
        return await RunListenCoreAsync();
    }
    catch (Exception ex)
    {
        LogToFile($"FATAL: {ex}");
        return 1;
    }
}

async Task<int> RunListenCoreAsync()
{
    var config = GetConfig();
    if (config is null)
    {
        LogToFile("Not configured — run SemaNami --setup first.");
        return 1;
    }

    var dbPath = GetDbPath();
    var dbDirectory = Path.GetDirectoryName(dbPath)!;
    Directory.CreateDirectory(dbDirectory);

    // Single-instance guard: the OS releases this automatically on any process exit (including
    // a hard kill), so no cleanup code is needed, and it stops a manually-started --listen from
    // racing an auto-registered scheduled instance.
    FileStream lockFile;
    try
    {
        lockFile = new FileStream(Path.Combine(dbDirectory, "listener.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    catch (IOException)
    {
        LogToFile("Another --listen instance already holds the lock file — exiting.");
        return 1;
    }

    using (lockFile)
    {
        var store = new SqliteConversationStore(dbPath);
        var updatesSource = new TelegramUpdatesSource(config.Value.BotToken, long.Parse(config.Value.ChatId));
        var realtimeNotifier = new InProcessRealtimeNotifier();
        var listener = new ConversationListener(store, updatesSource, realtimeNotifier);
        var pipeServer = new PipeSubscriptionServer(new ConversationWaitResolver(store, realtimeNotifier));

        using var cts = new CancellationTokenSource();
        try
        {
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        }
        catch
        {
            // No console attached (e.g. launched by Task Scheduler) — Ctrl+C handling is
            // meaningless there anyway; the task's own stop/kill still terminates the process.
        }

        LogToFile("SemaNami listener started.");

        var pipeTask = pipeServer.RunAsync(cts.Token);

        while (!cts.IsCancellationRequested)
        {
            try
            {
                await listener.PollOnceAsync(cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogToFile($"Poll failed: {ex}");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        try
        {
            await pipeTask;
        }
        catch
        {
            // Shutting down — nothing to recover.
        }

        LogToFile("SemaNami listener stopping.");
        return 0;
    }
}

async Task<int> RunWaitForReplyAsync(string sender, string conversationId, long? after, int timeoutSeconds)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

    NamedPipeClientStream? pipeClient = null;
    try
    {
        pipeClient = new NamedPipeClientStream(".", PipeProtocol.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        connectCts.CancelAfter(TimeSpan.FromSeconds(2));
        await pipeClient.ConnectAsync(connectCts.Token);
    }
    catch
    {
        // Genuinely couldn't reach the daemon (not running, or connect timed out) — degrade to
        // polling below.
        pipeClient?.Dispose();
        pipeClient = null;
    }

    if (pipeClient is not null)
    {
        using (pipeClient)
        {
            try
            {
                using var writer = new StreamWriter(pipeClient, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(pipeClient, leaveOpen: true);

                await writer.WriteLineAsync(JsonSerializer.Serialize(new PipeSubscribeRequest(sender, conversationId, after)));

                var responseLine = await reader.ReadLineAsync(cts.Token);
                if (!string.IsNullOrEmpty(responseLine))
                {
                    var response = JsonSerializer.Deserialize<PipeSubscribeResponse>(responseLine);
                    if (response is not null && response.Messages.Count > 0)
                    {
                        Console.WriteLine(JsonSerializer.Serialize(response.Messages));
                        return 0;
                    }
                }

                // Connected fine and used the realtime path — it just found nothing before our
                // own --timeout. That is a clean, expected outcome, not an unreachable listener,
                // and polling further is pointless: our whole time budget is already spent.
                return 2;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                return 2;
            }
            catch
            {
                // Connected, then lost the daemon mid-wait (e.g. it restarted) — fall back to
                // polling for whatever time budget remains.
                Console.Error.WriteLine("Lost connection to the SemaNami listener mid-wait — falling back to polling.");
            }
        }
    }
    else
    {
        Console.Error.WriteLine("SemaNami listener not reachable — falling back to polling.");
    }

    var store = new SqliteConversationStore(GetDbPath());
    while (!cts.IsCancellationRequested)
    {
        var history = store.GetHistory(sender, conversationId, after)
            .Where(m => m.Direction == "received")
            .ToList();
        if (history.Count > 0)
        {
            Console.WriteLine(JsonSerializer.Serialize(history));
            return 0;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }

    return 2;
}

namespace SemaNami.Core;

// macOS/Linux have no per-user registry equivalent, so config lives in a small dedicated env
// file, sourced from whichever shell rc files the user actually has (idempotently — re-running
// setup never duplicates the source line).
public sealed class UnixEnvironmentPersister : IEnvironmentPersister
{
    private const string BotTokenVar = "TELEGRAM_BOT_TOKEN";
    private const string ChatIdVar = "TELEGRAM_CHAT_ID";

    private readonly string envFilePath;
    private readonly IReadOnlyList<string> rcFilePaths;

    public UnixEnvironmentPersister()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".semanami", "env.sh"),
            new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bashrc"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zshrc"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".profile"),
            })
    {
    }

    // Internal, explicit-paths constructor for unit tests — avoids touching the real home
    // directory's shell rc files.
    internal UnixEnvironmentPersister(string envFilePath, IReadOnlyList<string> rcFilePaths)
    {
        this.envFilePath = envFilePath;
        this.rcFilePaths = rcFilePaths;
    }

    public void Persist(string botToken, string chatId)
    {
        var directory = Path.GetDirectoryName(envFilePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(envFilePath, $"export {BotTokenVar}=\"{botToken}\"\nexport {ChatIdVar}=\"{chatId}\"\n");

        var sourceLine = $". \"{envFilePath}\"";
        foreach (var rcFile in rcFilePaths)
        {
            if (!File.Exists(rcFile))
            {
                continue;
            }

            var contents = File.ReadAllText(rcFile);
            if (!contents.Contains(sourceLine))
            {
                File.AppendAllText(rcFile, $"\n# Added by SemaNami\n{sourceLine}\n");
            }
        }
    }

    public (string? BotToken, string? ChatId) ReadExisting()
    {
        if (!File.Exists(envFilePath))
        {
            return (null, null);
        }

        var values = ParseEnvFile(envFilePath);
        var token = values.GetValueOrDefault(BotTokenVar);
        var chatId = values.GetValueOrDefault(ChatIdVar);
        return (
            string.IsNullOrWhiteSpace(token) ? null : token,
            string.IsNullOrWhiteSpace(chatId) ? null : chatId);
    }

    private static Dictionary<string, string> ParseEnvFile(string path)
    {
        var result = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("export "))
            {
                continue;
            }

            var rest = trimmed["export ".Length..];
            var equalsIndex = rest.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var key = rest[..equalsIndex];
            var value = rest[(equalsIndex + 1)..].Trim('"');
            result[key] = value;
        }

        return result;
    }
}

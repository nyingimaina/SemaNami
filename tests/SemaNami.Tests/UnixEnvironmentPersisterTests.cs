using SemaNami.Core;
using Xunit;

namespace SemaNami.Tests;

public class UnixEnvironmentPersisterTests : IDisposable
{
    private readonly string tempDir;
    private readonly string envFilePath;
    private readonly string rcFilePath;

    public UnixEnvironmentPersisterTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "semanami-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        envFilePath = Path.Combine(tempDir, "env.sh");
        rcFilePath = Path.Combine(tempDir, ".bashrc");
        File.WriteAllText(rcFilePath, "# existing rc content\n");
    }

    public void Dispose() => Directory.Delete(tempDir, recursive: true);

    private UnixEnvironmentPersister CreatePersister() => new(envFilePath, new[] { rcFilePath });

    [Fact]
    public void Persist_WritesTokenAndChatIdToEnvFile()
    {
        CreatePersister().Persist("123:abc", "999");

        var contents = File.ReadAllText(envFilePath);
        Assert.Contains("TELEGRAM_BOT_TOKEN=\"123:abc\"", contents);
        Assert.Contains("TELEGRAM_CHAT_ID=\"999\"", contents);
    }

    [Fact]
    public void Persist_AppendsSourceLineToRcFile()
    {
        CreatePersister().Persist("123:abc", "999");

        var rcContents = File.ReadAllText(rcFilePath);
        Assert.Contains(envFilePath, rcContents);
        Assert.Contains("# existing rc content", rcContents);
    }

    [Fact]
    public void Persist_CalledTwice_DoesNotDuplicateSourceLineInRcFile()
    {
        var persister = CreatePersister();
        persister.Persist("123:abc", "999");
        persister.Persist("123:abc", "999");

        var rcContents = File.ReadAllText(rcFilePath);
        var occurrences = rcContents.Split(envFilePath).Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void Persist_RcFileDoesNotExist_IsSkippedWithoutThrowing()
    {
        var missingRc = Path.Combine(tempDir, ".zshrc-does-not-exist");
        var persister = new UnixEnvironmentPersister(envFilePath, new[] { missingRc });

        var exception = Record.Exception(() => persister.Persist("123:abc", "999"));

        Assert.Null(exception);
        Assert.False(File.Exists(missingRc));
    }

    [Fact]
    public void ReadExisting_NothingPersistedYet_ReturnsNulls()
    {
        var result = CreatePersister().ReadExisting();

        Assert.Null(result.BotToken);
        Assert.Null(result.ChatId);
    }

    [Fact]
    public void ReadExisting_AfterPersist_ReadsBackFromEnvFile()
    {
        var persister = CreatePersister();
        persister.Persist("123:abc", "999");

        var result = persister.ReadExisting();

        Assert.Equal("123:abc", result.BotToken);
        Assert.Equal("999", result.ChatId);
    }
}

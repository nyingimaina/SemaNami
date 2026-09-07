namespace SemaNami.Cli;

public abstract record CliCommand
{
    // ConversationId is null for today's unchanged fire-and-forget notification path; non-null
    // starts or continues a multi-turn conversation.
    public sealed record Send(string Sender, string Message, string? ConversationId = null) : CliCommand;
    public sealed record ConversationHistory(string Sender, string ConversationId, long? After) : CliCommand;
    public sealed record CloseConversation(string Sender, string ConversationId) : CliCommand;
    public sealed record WaitForReply(string Sender, string ConversationId, long? After, int TimeoutSeconds) : CliCommand;
    public sealed record Listen : CliCommand;
    public sealed record InstallService : CliCommand;
    public sealed record UninstallService : CliCommand;
    public sealed record GetChatId : CliCommand;
    public sealed record RunSetup : CliCommand;
    public sealed record ShowUsage : CliCommand;
    public sealed record Invalid(string Error) : CliCommand;
}

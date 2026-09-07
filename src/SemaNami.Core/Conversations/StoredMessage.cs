namespace SemaNami.Core.Conversations;

public sealed record StoredMessage(
    long Seq,
    string Direction,
    int TelegramMessageId,
    string Text,
    bool AmbiguousMatch,
    DateTime CreatedAtUtc);

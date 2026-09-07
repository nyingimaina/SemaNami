namespace SemaNami.Core.Conversations;

public sealed record Conversation(
    string Sender,
    string ConversationId,
    string Status,
    int? LastMessageId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

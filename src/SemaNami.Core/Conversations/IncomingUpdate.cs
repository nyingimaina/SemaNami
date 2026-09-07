namespace SemaNami.Core.Conversations;

public sealed record IncomingUpdate(int UpdateId, long ChatId, int MessageId, int? ReplyToMessageId, string Text);

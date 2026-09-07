namespace SemaNami.Core.Conversations;

public static class PipeProtocol
{
    public const string PipeName = "SemaNami.Listen";
}

public sealed record PipeSubscribeRequest(string Sender, string ConversationId, long? AfterSeq);

public sealed record PipeSubscribeResponse(IReadOnlyList<StoredMessage> Messages);

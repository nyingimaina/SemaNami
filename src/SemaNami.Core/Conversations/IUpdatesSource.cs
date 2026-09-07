namespace SemaNami.Core.Conversations;

public interface IUpdatesSource
{
    Task<IReadOnlyList<IncomingUpdate>> GetUpdatesAsync(int? offset, int timeoutSeconds, CancellationToken cancellationToken = default);
}

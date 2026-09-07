namespace SemaNami.Core.Conversations;

public sealed class InProcessRealtimeNotifier : IRealtimeNotifier
{
    private readonly object gate = new();
    private readonly Dictionary<(string Sender, string ConversationId), List<TaskCompletionSource<StoredMessage>>> waiters = new();

    public void PublishNewMessage(string sender, string conversationId, StoredMessage message)
    {
        List<TaskCompletionSource<StoredMessage>>? toResolve = null;
        var key = (sender, conversationId);

        lock (gate)
        {
            if (waiters.TryGetValue(key, out var list))
            {
                toResolve = list;
                waiters.Remove(key);
            }
        }

        if (toResolve is null)
        {
            return;
        }

        foreach (var waiter in toResolve)
        {
            waiter.TrySetResult(message);
        }
    }

    public async Task<StoredMessage?> SubscribeAsync(string sender, string conversationId, CancellationToken cancellationToken)
    {
        var key = (sender, conversationId);
        var tcs = new TaskCompletionSource<StoredMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (gate)
        {
            if (!waiters.TryGetValue(key, out var list))
            {
                list = new List<TaskCompletionSource<StoredMessage>>();
                waiters[key] = list;
            }
            list.Add(tcs);
        }

        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());
        try
        {
            return await tcs.Task;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        finally
        {
            lock (gate)
            {
                if (waiters.TryGetValue(key, out var list))
                {
                    list.Remove(tcs);
                    if (list.Count == 0)
                    {
                        waiters.Remove(key);
                    }
                }
            }
        }
    }
}

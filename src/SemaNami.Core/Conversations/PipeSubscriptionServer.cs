using System.IO.Pipes;
using System.Text.Json;

namespace SemaNami.Core.Conversations;

// Thin transport — hosts the --listen process's named pipe and translates each connection into
// a ConversationWaitResolver.ResolveAsync call. Not heavily unit tested (same convention as
// TelegramBotMessageSender); the logic it delegates to is fully covered by
// ConversationWaitResolverTests. Cross-platform via System.IO.Pipes (real named pipes on
// Windows; Unix domain sockets under the hood on macOS/Linux) with no per-OS code needed here.
public sealed class PipeSubscriptionServer
{
    private static readonly TimeSpan MaxConnectionLifetime = TimeSpan.FromMinutes(10);

    private readonly ConversationWaitResolver resolver;

    public PipeSubscriptionServer(ConversationWaitResolver resolver)
    {
        this.resolver = resolver;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                PipeProtocol.PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }

            _ = HandleConnectionAsync(pipe, cancellationToken);
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken serverShuttingDown)
    {
        using (pipe)
        {
            try
            {
                using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(serverShuttingDown);
                connectionTimeout.CancelAfter(MaxConnectionLifetime);

                using var reader = new StreamReader(pipe, leaveOpen: true);
                using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };

                var requestLine = await reader.ReadLineAsync(connectionTimeout.Token);
                if (string.IsNullOrEmpty(requestLine))
                {
                    return;
                }

                var request = JsonSerializer.Deserialize<PipeSubscribeRequest>(requestLine);
                if (request is null)
                {
                    return;
                }

                var messages = await resolver.ResolveAsync(request.Sender, request.ConversationId, request.AfterSeq, connectionTimeout.Token);
                await writer.WriteLineAsync(JsonSerializer.Serialize(new PipeSubscribeResponse(messages)));
            }
            catch
            {
                // Client disconnected, timed out, or sent garbage — nothing to recover; the
                // message (if any) is already safely durable regardless of whether this
                // particular connection ever got to hear about it.
            }
        }
    }
}

using Telegram.Bot.Types;
using TelegramNotifier.Core;
using Xunit;

namespace TelegramNotifier.Tests;

public class ChatIdExtractionTests
{
    private static Update UpdateForChat(long chatId) => new()
    {
        Message = new Message { Chat = new Chat { Id = chatId } },
    };

    [Fact]
    public void ExtractDistinctChatIds_MultipleUpdatesSameChat_ReturnsOneEntry()
    {
        var updates = new[] { UpdateForChat(111), UpdateForChat(111) };

        var result = TelegramChatIdLookup.ExtractDistinctChatIds(updates);

        Assert.Equal(new long[] { 111 }, result);
    }

    [Fact]
    public void ExtractDistinctChatIds_DifferentChats_ReturnsAllDistinctIds()
    {
        var updates = new[] { UpdateForChat(111), UpdateForChat(222) };

        var result = TelegramChatIdLookup.ExtractDistinctChatIds(updates);

        Assert.Equal(new long[] { 111, 222 }, result);
    }

    [Fact]
    public void ExtractDistinctChatIds_UpdateWithNoMessage_IsIgnored()
    {
        var updates = new[] { new Update(), UpdateForChat(111) };

        var result = TelegramChatIdLookup.ExtractDistinctChatIds(updates);

        Assert.Equal(new long[] { 111 }, result);
    }

    [Fact]
    public void ExtractDistinctChatIds_NoUpdates_ReturnsEmpty()
    {
        var result = TelegramChatIdLookup.ExtractDistinctChatIds(Array.Empty<Update>());

        Assert.Empty(result);
    }
}

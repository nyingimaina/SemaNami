namespace SemaNami.Core;

public interface IBotValidator
{
    // Returns the bot's @username if the token is valid, null otherwise.
    Task<string?> TryGetBotUsernameAsync(string botToken, CancellationToken cancellationToken = default);
}

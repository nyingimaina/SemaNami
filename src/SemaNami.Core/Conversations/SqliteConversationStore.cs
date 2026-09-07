using Microsoft.Data.Sqlite;

namespace SemaNami.Core.Conversations;

public sealed class SqliteConversationStore : IConversationStore
{
    private readonly string connectionString;

    public SqliteConversationStore(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Pooling disabled: every operation here already opens and disposes its own short-lived
        // connection, so pooling buys nothing — and leaving it on holds a native handle open
        // after Dispose(), which fights attempts to delete the file (e.g. in test teardown).
        connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();
        InitializeSchema();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void InitializeSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS conversations (
                row_id           INTEGER PRIMARY KEY AUTOINCREMENT,
                sender           TEXT NOT NULL,
                conversation_id  TEXT NOT NULL,
                status           TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open', 'closed')),
                last_message_id  INTEGER NULL,
                created_at_utc   TEXT NOT NULL,
                updated_at_utc   TEXT NOT NULL,
                UNIQUE (sender, conversation_id)
            );

            CREATE TABLE IF NOT EXISTS messages (
                seq                  INTEGER PRIMARY KEY AUTOINCREMENT,
                conversation_row_id  INTEGER NOT NULL REFERENCES conversations(row_id),
                direction            TEXT NOT NULL CHECK (direction IN ('sent', 'received')),
                telegram_message_id  INTEGER NOT NULL,
                text                 TEXT NOT NULL,
                ambiguous_match      INTEGER NOT NULL DEFAULT 0,
                created_at_utc       TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_messages_telegram_message_id ON messages(telegram_message_id);
            CREATE INDEX IF NOT EXISTS ix_messages_conversation_seq ON messages(conversation_row_id, seq);

            CREATE TABLE IF NOT EXISTS listener_state (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public Conversation? GetConversation(string sender, string conversationId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT sender, conversation_id, status, last_message_id, created_at_utc, updated_at_utc " +
            "FROM conversations WHERE sender = @sender AND conversation_id = @conversationId";
        command.Parameters.AddWithValue("@sender", sender);
        command.Parameters.AddWithValue("@conversationId", conversationId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadConversation(reader) : null;
    }

    public void RecordSentMessage(string sender, string conversationId, int telegramMessageId, string text)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var rowId = UpsertConversation(connection, transaction, sender, conversationId, telegramMessageId);
        InsertMessageIgnoringDuplicates(connection, transaction, rowId, "sent", telegramMessageId, text, ambiguousMatch: false);

        transaction.Commit();
    }

    public StoredMessage RecordReceivedMessage(string sender, string conversationId, int telegramMessageId, string text, bool ambiguousMatch)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var rowId = UpsertConversation(connection, transaction, sender, conversationId, telegramMessageId);
        InsertMessageIgnoringDuplicates(connection, transaction, rowId, "received", telegramMessageId, text, ambiguousMatch);
        var stored = SelectMessageByTelegramId(connection, transaction, telegramMessageId);

        transaction.Commit();
        return stored;
    }

    private static StoredMessage SelectMessageByTelegramId(SqliteConnection connection, SqliteTransaction transaction, int telegramMessageId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT seq, direction, telegram_message_id, text, ambiguous_match, created_at_utc " +
            "FROM messages WHERE telegram_message_id = @telegramMessageId";
        command.Parameters.AddWithValue("@telegramMessageId", telegramMessageId);

        using var reader = command.ExecuteReader();
        reader.Read();
        return new StoredMessage(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetInt32(4) != 0,
            DateTime.Parse(reader.GetString(5)).ToUniversalTime());
    }

    public (string Sender, string ConversationId)? TryFindConversationByMessageId(int telegramMessageId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT c.sender, c.conversation_id " +
            "FROM messages m JOIN conversations c ON c.row_id = m.conversation_row_id " +
            "WHERE m.telegram_message_id = @telegramMessageId";
        command.Parameters.AddWithValue("@telegramMessageId", telegramMessageId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    public IReadOnlyList<Conversation> GetOpenConversations()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT sender, conversation_id, status, last_message_id, created_at_utc, updated_at_utc " +
            "FROM conversations WHERE status = 'open'";

        using var reader = command.ExecuteReader();
        var results = new List<Conversation>();
        while (reader.Read())
        {
            results.Add(ReadConversation(reader));
        }
        return results;
    }

    public void CloseConversation(string sender, string conversationId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE conversations SET status = 'closed', updated_at_utc = @now " +
            "WHERE sender = @sender AND conversation_id = @conversationId";
        command.Parameters.AddWithValue("@sender", sender);
        command.Parameters.AddWithValue("@conversationId", conversationId);
        command.Parameters.AddWithValue("@now", UtcNowText());
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<StoredMessage> GetHistory(string sender, string conversationId, long? afterSeq)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT m.seq, m.direction, m.telegram_message_id, m.text, m.ambiguous_match, m.created_at_utc " +
            "FROM messages m JOIN conversations c ON c.row_id = m.conversation_row_id " +
            "WHERE c.sender = @sender AND c.conversation_id = @conversationId " +
            (afterSeq.HasValue ? "AND m.seq > @afterSeq " : "") +
            "ORDER BY m.seq ASC";
        command.Parameters.AddWithValue("@sender", sender);
        command.Parameters.AddWithValue("@conversationId", conversationId);
        if (afterSeq.HasValue)
        {
            command.Parameters.AddWithValue("@afterSeq", afterSeq.Value);
        }

        using var reader = command.ExecuteReader();
        var results = new List<StoredMessage>();
        while (reader.Read())
        {
            results.Add(new StoredMessage(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt32(4) != 0,
                DateTime.Parse(reader.GetString(5)).ToUniversalTime()));
        }
        return results;
    }

    public int? GetLastUpdateId()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM listener_state WHERE key = 'last_update_id'";
        var result = command.ExecuteScalar();
        return result is null ? null : int.Parse((string)result);
    }

    public void SetLastUpdateId(int updateId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO listener_state (key, value) VALUES ('last_update_id', @value) " +
            "ON CONFLICT(key) DO UPDATE SET value = @value";
        command.Parameters.AddWithValue("@value", updateId.ToString());
        command.ExecuteNonQuery();
    }

    private static long UpsertConversation(SqliteConnection connection, SqliteTransaction transaction, string sender, string conversationId, int lastMessageId)
    {
        var now = UtcNowText();
        using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText = """
                INSERT INTO conversations (sender, conversation_id, status, last_message_id, created_at_utc, updated_at_utc)
                VALUES (@sender, @conversationId, 'open', @lastMessageId, @now, @now)
                ON CONFLICT(sender, conversation_id) DO UPDATE SET
                    last_message_id = @lastMessageId,
                    updated_at_utc = @now,
                    status = 'open'
                """;
            upsert.Parameters.AddWithValue("@sender", sender);
            upsert.Parameters.AddWithValue("@conversationId", conversationId);
            upsert.Parameters.AddWithValue("@lastMessageId", lastMessageId);
            upsert.Parameters.AddWithValue("@now", now);
            upsert.ExecuteNonQuery();
        }

        using var select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT row_id FROM conversations WHERE sender = @sender AND conversation_id = @conversationId";
        select.Parameters.AddWithValue("@sender", sender);
        select.Parameters.AddWithValue("@conversationId", conversationId);
        return (long)select.ExecuteScalar()!;
    }

    private static void InsertMessageIgnoringDuplicates(
        SqliteConnection connection, SqliteTransaction transaction, long conversationRowId,
        string direction, int telegramMessageId, string text, bool ambiguousMatch)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // INSERT OR IGNORE: the UNIQUE index on telegram_message_id makes replaying an
        // already-processed Telegram update after a restart a safe no-op instead of a duplicate
        // row or a thrown constraint-violation exception.
        command.CommandText = """
            INSERT OR IGNORE INTO messages (conversation_row_id, direction, telegram_message_id, text, ambiguous_match, created_at_utc)
            VALUES (@conversationRowId, @direction, @telegramMessageId, @text, @ambiguousMatch, @now)
            """;
        command.Parameters.AddWithValue("@conversationRowId", conversationRowId);
        command.Parameters.AddWithValue("@direction", direction);
        command.Parameters.AddWithValue("@telegramMessageId", telegramMessageId);
        command.Parameters.AddWithValue("@text", text);
        command.Parameters.AddWithValue("@ambiguousMatch", ambiguousMatch ? 1 : 0);
        command.Parameters.AddWithValue("@now", UtcNowText());
        command.ExecuteNonQuery();
    }

    private static Conversation ReadConversation(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetInt32(3),
        DateTime.Parse(reader.GetString(4)).ToUniversalTime(),
        DateTime.Parse(reader.GetString(5)).ToUniversalTime());

    private static string UtcNowText() => DateTime.UtcNow.ToString("O");
}

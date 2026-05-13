using Microsoft.Data.Sqlite;

namespace FightNet.Server.Database;

public class DbContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DbContext(string dbPath = "fightnet.db")
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        await ExecAsync("""
            CREATE TABLE IF NOT EXISTS Users (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Username     TEXT    NOT NULL UNIQUE,
                PasswordHash TEXT    NOT NULL,
                Wins         INTEGER NOT NULL DEFAULT 0,
                Losses       INTEGER NOT NULL DEFAULT 0,
                CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
            )
            """);

        await ExecAsync("""
            CREATE TABLE IF NOT EXISTS MatchHistory (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                WinnerId        INTEGER NOT NULL REFERENCES Users(Id),
                LoserId         INTEGER NOT NULL REFERENCES Users(Id),
                DurationSeconds INTEGER NOT NULL,
                PlayedAt        TEXT    NOT NULL DEFAULT (datetime('now'))
            )
            """);
    }

    // ── auth ──────────────────────────────────────────────────────────────────

    // Returns the new user's id, or -1 if the username is already taken.
    public async Task<int> RegisterAsync(string username, string password)
    {
        string hash = BCrypt.Net.BCrypt.HashPassword(password);
        await _lock.WaitAsync();
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @h); SELECT last_insert_rowid()";
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@h", hash);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (SqliteException)
        {
            return -1; // duplicate username
        }
        finally { _lock.Release(); }
    }

    // Returns the user's id on success, -1 on wrong credentials.
    public async Task<int> LoginAsync(string username, string password)
    {
        await _lock.WaitAsync();
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, PasswordHash FROM Users WHERE Username = @u";
            cmd.Parameters.AddWithValue("@u", username);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return -1;

            int id       = reader.GetInt32(0);
            string hash  = reader.GetString(1);
            return BCrypt.Net.BCrypt.Verify(password, hash) ? id : -1;
        }
        finally { _lock.Release(); }
    }

    // ── match history ─────────────────────────────────────────────────────────

    public async Task RecordMatchAsync(int winnerId, int loserId, int durationSeconds)
    {
        await _lock.WaitAsync();
        try
        {
            using var tx = _connection.BeginTransaction();

            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO MatchHistory (WinnerId, LoserId, DurationSeconds) VALUES (@w, @l, @d)";
                cmd.Parameters.AddWithValue("@w", winnerId);
                cmd.Parameters.AddWithValue("@l", loserId);
                cmd.Parameters.AddWithValue("@d", durationSeconds);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE Users SET Wins = Wins + 1 WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", winnerId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = _connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE Users SET Losses = Losses + 1 WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", loserId);
                await cmd.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        finally { _lock.Release(); }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task ExecAsync(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose() => _connection.Dispose();
}

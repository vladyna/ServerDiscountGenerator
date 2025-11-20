using Dapper;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace DiscountServer.Data
{
    public class DiscountRepository
    {
        #region Fields
        private readonly string _cs;
        #endregion

        #region Ctor
        public DiscountRepository(string path)
        {
            _cs = $"Data Source={path}";
            Initialize();
        }
        #endregion

        #region Init
        private void Initialize()
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    using var conn = new SqliteConnection(_cs);
                    conn.Open();
                    conn.Execute(@"CREATE TABLE IF NOT EXISTS DiscountCodes(
                        Code TEXT PRIMARY KEY,
                        Length INTEGER NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        Used INTEGER NOT NULL DEFAULT 0,
                        UsedAt TEXT NULL
                    );");
                    conn.Execute("PRAGMA journal_mode=WAL;");
                    break;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == raw.SQLITE_BUSY && retries++ < 5)
                {
                    var delay = 50 * (int)Math.Pow(2, retries - 1);
                    Thread.Sleep(delay);
                }
            }
        }
        #endregion

        #region Connection
        private async Task<SqliteConnection> OpenConnectionWithRetryAsync()
        {
            var conn = new SqliteConnection(_cs);
            int retries = 0;
            while (true)
            {
                try
                {
                    await conn.OpenAsync();
                    return conn;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == raw.SQLITE_BUSY && retries++ < 5)
                {
                    var delay = 50 * (int)Math.Pow(2, retries - 1);
                    await Task.Delay(delay);
                }
            }
        }
        #endregion

        #region Insert
        public async Task<IEnumerable<string>> InsertCodesAsync(IEnumerable<string> codes, int length)
        {
            var pending = new HashSet<string>(codes);
            var insertedAll = new HashSet<string>();
            const int maxCycles = 40;
            int cycle = 0;
            var sql = "INSERT OR IGNORE INTO DiscountCodes(Code, Length, CreatedAt) VALUES(@c, @l, CURRENT_TIMESTAMP)";

            while (pending.Count > 0 && cycle < maxCycles)
            {
                cycle++;
                using var conn = await OpenConnectionWithRetryAsync();
                using var t = conn.BeginTransaction();
                var toRetry = new List<string>();
                foreach (var code in pending)
                {
                    int stmtRetries = 0;
                    while (true)
                    {
                        try
                        {
                            var ok = await conn.ExecuteAsync(sql, new { c = code, l = length }, t);
                            if (ok == 1) insertedAll.Add(code);
                            break;
                        }
                        catch (SqliteException ex) when (ex.SqliteErrorCode == raw.SQLITE_BUSY && stmtRetries++ < 3)
                        {
                            var delay = 10 * (int)Math.Pow(2, stmtRetries - 1);
                            await Task.Delay(delay);
                            continue;
                        }
                        catch (SqliteException ex) when (ex.SqliteErrorCode == raw.SQLITE_BUSY)
                        {
                            toRetry.Add(code);
                            break;
                        }
                    }
                }
                try 
                { 
                    t.Commit(); 
                }
                catch 
                { 
                }

                foreach (var code in insertedAll) 
                    pending.Remove(code);

                var duplicates = pending.Where(c => !toRetry.Contains(c) && !insertedAll.Contains(c)).ToList();
                foreach (var d in duplicates) 
                    pending.Remove(d);

                if (cycle % 5 == 0 && toRetry.Count == pending.Count) 
                    await Task.Delay(25);
            }
            return insertedAll;
        }
        #endregion

        #region Query
        public async Task<IReadOnlyList<string>> ListCodesAsync(int? length, int limit)
        {
            using var conn = await OpenConnectionWithRetryAsync();
            var sql = length.HasValue ?
                "SELECT Code FROM DiscountCodes WHERE Length=@l ORDER BY CreatedAt DESC LIMIT @limit" :
                "SELECT Code FROM DiscountCodes ORDER BY CreatedAt DESC LIMIT @limit";
            var rows = await conn.QueryAsync<string>(sql, new { l = length, limit });
            return rows.ToList();
        }
        #endregion

        #region Use
        public async Task<int> UseCodeAsync(string code)
        {
            using var conn = await OpenConnectionWithRetryAsync();
            var rows = await conn.ExecuteAsync(
                "UPDATE DiscountCodes SET Used = 1, UsedAt=CURRENT_TIMESTAMP WHERE Code=@c AND Used=0",
                new { c = code }
            );
            if (rows == 1) return 0;
            var exists = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT Used FROM DiscountCodes WHERE Code=@c",
                new { c = code }
            );
            if (exists == null) return 1;
            return 2;
        }
        #endregion
    }
}

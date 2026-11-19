using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using tts.Data;

if (AppTransactions.Isolation != System.Data.IsolationLevel.ReadCommitted) throw new Exception("Workflow isolation must support fresh reads after account/task locks.");
if (!args.Contains("--database")) { Console.WriteLine("PASS: transaction policy. Add --database for read-only connection, isolation, locking and model/schema checks."); return; }
var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", true).AddJsonFile("appsettings.Local.json", true).AddEnvironmentVariables().Build();
try {
    var connectionOptions = new MySqlConnectionStringBuilder((config.GetConnectionString("ApplicationDatabase") ?? throw new InvalidOperationException("Configure ConnectionStrings:ApplicationDatabase for the read-only probe."))) { AllowUserVariables = true, UseAffectedRows = false };
    await using var connection = new MySqlConnection(connectionOptions.ConnectionString);
    await connection.OpenAsync();
    async Task<string> Scalar(string sql) { await using var command = new MySqlCommand(sql, connection); return Convert.ToString(await command.ExecuteScalarAsync()) ?? ""; }
    var version = await Scalar("SELECT VERSION()");
    Console.WriteLine("Database version: " + version);
    var tidb = version.Contains("TiDB", StringComparison.OrdinalIgnoreCase);
    if (tidb) {
        var mode = await Scalar("SELECT @@tidb_txn_mode");
        Console.WriteLine("TiDB transaction mode: " + mode);
        if (!mode.Equals("pessimistic", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Workflow locks require pessimistic transactions; review TiDB session configuration.");
    } else Console.WriteLine("NOTE: configured database is not identified as TiDB; this does not verify the Render database.");
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 36))).Options;
    await using var db = new ApplicationDbContext(options);
    await using (var transaction = await db.Database.BeginTransactionAsync(AppTransactions.Isolation)) {
        await db.Users.FromSqlRaw("SELECT * FROM tbl_user WHERE Id=-1 FOR UPDATE").AsNoTracking().ToListAsync();
        await AppTransactions.LockTaskAsync(db, -1, default);
        await transaction.RollbackAsync();
    }
    Console.WriteLine("PASS: READ COMMITTED and account/task locking queries; rolled back without writes.");
    var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await using (var command = new MySqlCommand("SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()", connection)) {
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) actual.Add(reader.GetString(0) + "." + reader.GetString(1));
    }
    var missing = new List<string>();
    foreach (var entity in db.Model.GetEntityTypes()) {
        if (entity.GetTableName() is not { } table) continue;
        var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
        foreach (var property in entity.GetProperties()) {
            var column = property.GetColumnName(store);
            if (column is not null && !actual.Contains(table + "." + column)) missing.Add(table + "." + column);
        }
    }
    foreach (var name in missing.Distinct()) Console.WriteLine("MISSING: " + name);
    Console.WriteLine(missing.Count == 0 ? "PASS: all EF-mapped table/column names exist (types, indexes and constraints not checked)." : "FAIL: schema requires matching migrations; no schema changes were applied.");
    if (missing.Count > 0) Environment.ExitCode = 1;
} catch (MySqlException ex) { Console.WriteLine("Database probe failed; MySQL error code=" + ex.Number); Environment.ExitCode = 1; }



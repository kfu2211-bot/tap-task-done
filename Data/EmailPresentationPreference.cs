using Microsoft.EntityFrameworkCore;
namespace tts.Data;
public sealed class EmailPresentationPreference
{
    public int UserId { get; set; }
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "light";
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS email_presentation_preferences (
          UserId INT NOT NULL PRIMARY KEY, Language VARCHAR(3) NOT NULL DEFAULT 'en', Theme VARCHAR(5) NOT NULL DEFAULT 'light',
          CONSTRAINT FK_email_preference_user FOREIGN KEY (UserId) REFERENCES tbl_user(Id) ON DELETE CASCADE
        ) ENGINE=InnoDB;
        """;
    public static async Task EnsureAsync(ApplicationDbContext db, CancellationToken ct)
    {
        try { await db.Database.ExecuteSqlRawAsync(SchemaSql, ct); }
        catch (MySqlConnector.MySqlException) { await db.Set<EmailPresentationPreference>().AsNoTracking().Take(1).ToListAsync(ct); }
    }
}

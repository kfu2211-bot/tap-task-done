using Microsoft.EntityFrameworkCore;

namespace tts.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();
        user.ToTable("tbl_user");
        user.HasKey(x => x.Id);
        user.Property(x => x.Id).HasColumnName("Id").ValueGeneratedOnAdd();
        user.Property(x => x.FirstName).HasColumnName("FirstName").HasMaxLength(45);
        user.Property(x => x.LastName).HasColumnName("LastName").HasMaxLength(45);
        user.Property(x => x.Username).HasColumnName("userName").HasMaxLength(45);
        user.Property(x => x.Role).HasColumnName("role").HasMaxLength(45);
        user.Property(x => x.PasswordHash).HasColumnName("Password").HasColumnType("blob");
    }
}

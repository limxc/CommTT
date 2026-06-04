using CommTT.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace CommTT.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CommDataFrameEntity> Frames => Set<CommDataFrameEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
            options.UseSqlite("Data Source=commtt.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommDataFrameEntity>().Property(e => e.Raw).HasMaxLength(8192);
    }
}

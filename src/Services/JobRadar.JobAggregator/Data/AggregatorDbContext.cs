using JobRadar.JobAggregator.Models;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.JobAggregator.Data;

public class AggregatorDbContext(DbContextOptions<AggregatorDbContext> options) : DbContext(options)
{
    public DbSet<ActiveWatchEntity> ActiveWatches => Set<ActiveWatchEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActiveWatchEntity>(e =>
        {
            e.HasIndex(w => w.IsActive);
            e.Property(w => w.MinSalary).HasColumnType("numeric(10,2)");
        });
    }
}

using JobRadar.Matching.Models;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Matching.Data;

public class MatchingDbContext(DbContextOptions<MatchingDbContext> options) : DbContext(options)
{
    public DbSet<SeenJobEntity> SeenJobs => Set<SeenJobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SeenJobEntity>(e =>
        {
            e.HasIndex(s => new { s.UserId, s.JobDedupeKey }).IsUnique();
        });
    }
}

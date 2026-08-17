using JobRadar.Notifications.Models;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Notifications.Data;

public class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationEntity>(e =>
        {
            e.HasIndex(n => new { n.UserId, n.MatchedAt });
        });
    }
}

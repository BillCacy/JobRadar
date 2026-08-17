using JobRadar.Users.Models;
using Microsoft.EntityFrameworkCore;

namespace JobRadar.Users.Data;

public class UsersDbContext(DbContextOptions<UsersDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SearchCriteriaEntity> SearchCriteria => Set<SearchCriteriaEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<SearchCriteriaEntity>(e =>
        {
            e.HasIndex(c => c.UserId);
            e.Property(c => c.MinSalary).HasColumnType("numeric(10,2)");
        });
    }
}

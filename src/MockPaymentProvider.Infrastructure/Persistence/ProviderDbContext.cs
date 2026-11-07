using Microsoft.EntityFrameworkCore;
using MockPaymentProvider.Domain.Entities;

namespace MockPaymentProvider.Infrastructure.Persistence;

public class ProviderDbContext(DbContextOptions<ProviderDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // finds every IEntityTypeConfiguration in this project
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProviderDbContext).Assembly);
    }
}
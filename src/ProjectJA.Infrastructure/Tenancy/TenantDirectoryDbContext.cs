// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;

namespace ProjectJA.Infrastructure.Tenancy;

public sealed class TenantDirectoryDbContext : DbContext
{
    public TenantDirectoryDbContext(DbContextOptions<TenantDirectoryDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var t = modelBuilder.Entity<Tenant>();
        t.ToTable("tenants");
        t.HasKey(x => x.Id);
        t.Property(x => x.Slug).HasMaxLength(64).IsRequired();
        t.HasIndex(x => x.Slug).IsUnique();
        t.Property(x => x.Name).HasMaxLength(200).IsRequired();
        t.Property(x => x.ConnectionString).HasMaxLength(2000).IsRequired();
        t.Property(x => x.CreatedAt).IsRequired();
    }
}

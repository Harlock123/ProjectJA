// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectJA.Infrastructure.Tenancy;

public sealed class TenantDirectoryDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TenantDirectoryDbContext>
{
    public TenantDirectoryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TenantDirectoryDbContext>()
            .UseNpgsql("Host=localhost;Database=projectja_design;Username=postgres;Password=postgres")
            .Options;
        return new TenantDirectoryDbContext(options);
    }
}

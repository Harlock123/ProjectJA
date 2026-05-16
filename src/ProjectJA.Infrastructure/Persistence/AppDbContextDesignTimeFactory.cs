// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectJA.Infrastructure.Persistence;

/// <summary>
/// EF Core uses this at design time (e.g. `dotnet ef migrations add`) to build the model
/// without going through DI — which would otherwise require a resolved tenant.
/// The connection string here doesn't need to point at a real DB; only the model is read.
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=projectja_design;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}

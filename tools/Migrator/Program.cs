// SPDX-License-Identifier: BUSL-1.1
// ProjectJA per-tenant migration runner.
//
// Usage:
//   dotnet run --project tools/Migrator
//
// Applies pending migrations to the tenant directory first, then iterates every tenant
// in the directory and applies pending migrations to that tenant's AppDbContext.
// Run on every deploy before swapping app traffic.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.Infrastructure.Tenancy;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("src/ProjectJA.Host/appsettings.json", optional: true)
    .AddJsonFile("src/ProjectJA.Host/appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var directoryConn = config.GetConnectionString("TenantDirectory")
    ?? config.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings__TenantDirectory or ConnectionStrings__Default.");

Console.WriteLine("Applying tenant directory migrations...");
var directoryOptions = new DbContextOptionsBuilder<TenantDirectoryDbContext>()
    .UseNpgsql(directoryConn).Options;

await using var directory = new TenantDirectoryDbContext(directoryOptions);
await directory.Database.MigrateAsync();
Console.WriteLine("  ✓ directory up to date");

var tenants = await directory.Tenants.AsNoTracking().OrderBy(t => t.Slug).ToListAsync();
Console.WriteLine($"\nFound {tenants.Count} tenant(s):");

var failures = 0;
foreach (var t in tenants)
{
    Console.Write($"  {t.Slug,-20} ");
    try
    {
        var appOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(t.ConnectionString).Options;
        await using var db = new AppDbContext(appOptions);
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            Console.WriteLine("up to date");
            continue;
        }
        await db.Database.MigrateAsync();
        Console.WriteLine($"applied {pending.Count} migration(s)");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAILED: {ex.Message}");
    }
}

Console.WriteLine($"\n{tenants.Count - failures}/{tenants.Count} succeeded.");
return failures == 0 ? 0 : 1;

// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.Infrastructure.Tenancy;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Host.Endpoints;

public static class StartupBootstrap
{
    public static async Task EnsureSeededAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var directory = sp.GetRequiredService<TenantDirectoryDbContext>();
        await directory.Database.MigrateAsync();

        var defaultConnectionString = config.GetConnectionString("Default")
            ?? "Host=localhost;Database=projectja;Username=postgres;Password=postgres";

        if (!await directory.Tenants.AnyAsync())
        {
            directory.Tenants.Add(new Tenant
            {
                Id = Guid.NewGuid(),
                Slug = config["Bootstrap:TenantSlug"] ?? "default",
                Name = config["Bootstrap:OrgName"] ?? "Default Organization",
                ConnectionString = defaultConnectionString,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await directory.SaveChangesAsync();
        }

        // AppDbContext is now constructed from the tenant's connection string, so we
        // must set the scoped tenant before resolving it.
        var tenantDirectory = sp.GetRequiredService<ITenantDirectory>();
        var defaultTenant = await tenantDirectory.GetDefaultAsync(default)
            ?? throw new InvalidOperationException("Default tenant seeding failed.");
        sp.UseTenant(defaultTenant);

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.Organizations.AnyAsync())
        {
            var org = Organization.Create(
                slug: config["Bootstrap:OrgSlug"] ?? "default",
                name: config["Bootstrap:OrgName"] ?? "Default Organization",
                now: DateTimeOffset.UtcNow);
            db.Organizations.Add(org);
            await db.SaveChangesAsync();
        }

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (!await db.Users.AnyAsync())
        {
            var orgId = await db.Organizations.Select(o => o.Id).FirstAsync();
            var admin = new ApplicationUser
            {
                UserName = config["Bootstrap:AdminEmail"] ?? "admin@projectja.local",
                Email = config["Bootstrap:AdminEmail"] ?? "admin@projectja.local",
                FirstName = "Admin",
                LastName = "User",
                OrganizationId = orgId,
                CreatedAt = DateTimeOffset.UtcNow,
                EmailConfirmed = true
            };
            await users.CreateAsync(admin, config["Bootstrap:AdminPassword"] ?? "ChangeMe123!");
        }
    }
}

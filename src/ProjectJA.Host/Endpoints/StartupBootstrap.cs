// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.Infrastructure.Tenancy;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.Modules.Projects.Domain;
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
        var adminEmailSetting = config["Bootstrap:AdminEmail"] ?? "admin@projectja.local";
        if (!await db.Users.AnyAsync())
        {
            var orgId = await db.Organizations.Select(o => o.Id).FirstAsync();
            var admin = new ApplicationUser
            {
                UserName = adminEmailSetting,
                Email = adminEmailSetting,
                FirstName = "Admin",
                LastName = "User",
                OrganizationId = orgId,
                IsOrgAdmin = true,
                CreatedAt = DateTimeOffset.UtcNow,
                EmailConfirmed = true
            };
            await users.CreateAsync(admin, config["Bootstrap:AdminPassword"] ?? "ChangeMe123!");
        }

        // Self-heal: if migrations ran on an empty DB the IsOrgAdmin backfill
        // promotes nobody (no project_members yet), so the bootstrap admin can
        // be left with IsOrgAdmin=false and lose access to /invites etc. If
        // there's no OrgAdmin in the org, promote the bootstrap admin.
        if (!await db.Users.AnyAsync(u => u.IsOrgAdmin))
        {
            var bootstrapAdmin = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmailSetting);
            if (bootstrapAdmin is not null)
            {
                bootstrapAdmin.IsOrgAdmin = true;
                await db.SaveChangesAsync();
            }
        }

        // Backfill: projects created before membership existed have no members,
        // which would lock everyone out. Give every such project all current
        // users as Member + the bootstrap admin as Admin. Idempotent (only
        // touches projects with zero members). EntityState.Added is required
        // because adding an owned child with a set key to an already-tracked
        // aggregate is otherwise misclassified Modified (UPDATE → 0 rows).
        var allUserIds = await db.Users.Select(u => u.Id).ToListAsync();
        if (allUserIds.Count > 0)
        {
            var adminId = await db.Users.Where(u => u.Email == adminEmailSetting)
                .Select(u => u.Id).FirstOrDefaultAsync();
            if (adminId == Guid.Empty) adminId = allUserIds[0];

            var projects = await db.Projects.Include(p => p.Members).ToListAsync();
            var changed = false;
            foreach (var project in projects.Where(p => p.Members.Count == 0))
            {
                var now = DateTimeOffset.UtcNow;
                var adminMember = project.EnsureMember(adminId, ProjectRole.Admin, now);
                if (adminMember is not null) db.Entry(adminMember).State = EntityState.Added;
                foreach (var uid in allUserIds.Where(u => u != adminId))
                {
                    var member = project.EnsureMember(uid, ProjectRole.Member, now);
                    if (member is not null) db.Entry(member).State = EntityState.Added;
                }
                changed = true;
            }
            if (changed) await db.SaveChangesAsync();
        }
    }
}

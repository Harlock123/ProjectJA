// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.IntegrationTests.Fixtures;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.IntegrationTests.Tests;

[Collection(AppCollection.Name)]
public sealed class BootstrapTests(AppFactory factory)
{
    [Fact]
    public async Task Default_tenant_seeded_in_directory()
    {
        // Force WebApplicationFactory to start the host so bootstrap runs.
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenant = await directory.GetDefaultAsync(default);

        Assert.NotNull(tenant);
        Assert.Equal("default", tenant!.Slug);
    }

    [Fact]
    public async Task Default_organization_and_admin_user_seeded()
    {
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenant = await directory.GetDefaultAsync(default);
        scope.ServiceProvider.UseTenant(tenant!);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var org = await db.Organizations.FirstOrDefaultAsync();
        Assert.NotNull(org);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(AppFactory.AdminEmail);
        Assert.NotNull(admin);
        Assert.True(await users.CheckPasswordAsync(admin!, AppFactory.AdminPassword));
    }
}

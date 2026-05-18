// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectJA.IntegrationTests.Fixtures;

/// <summary>
/// Single collection fixture: owns the per-assembly Postgres container AND hosts
/// the app against it. xUnit v2 cannot inject one collection fixture into another's
/// constructor, so the container lives here rather than in a separate fixture.
/// xUnit calls <see cref="IAsyncLifetime.InitializeAsync"/> (container start) before
/// any test runs; <see cref="ConfigureWebHost"/> is invoked lazily on first
/// CreateClient/Server access, by which point the connection string is available.
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("projectja_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public const string AdminEmail = "admin@projectja.test";
    public const string AdminPassword = "TestPass123!";

    async Task IAsyncLifetime.InitializeAsync() => await _container.StartAsync();

    // Explicit impl: WebApplicationFactory already exposes a ValueTask DisposeAsync;
    // xUnit's IAsyncLifetime wants a Task DisposeAsync. Explicit interface avoids the
    // name clash and still lets xUnit drive teardown.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var cs = _container.GetConnectionString();
        builder.UseSetting("ConnectionStrings:Default", cs);
        builder.UseSetting("ConnectionStrings:TenantDirectory", cs);
        builder.UseSetting("Bootstrap:Enabled", "true");
        builder.UseSetting("Bootstrap:OrgSlug", "default");
        builder.UseSetting("Bootstrap:OrgName", "Test Organization");
        builder.UseSetting("Bootstrap:AdminEmail", AdminEmail);
        builder.UseSetting("Bootstrap:AdminPassword", AdminPassword);
        builder.UseSetting("Tenancy:Mode", "OnPrem");
    }
}

[CollectionDefinition(AppCollection.Name)]
public sealed class AppCollection : ICollectionFixture<AppFactory>
{
    public const string Name = "App";
}

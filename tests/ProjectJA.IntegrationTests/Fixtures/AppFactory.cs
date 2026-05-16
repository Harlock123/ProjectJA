// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ProjectJA.IntegrationTests.Fixtures;

/// <summary>
/// Hosts the app for tests against the shared Postgres container. ConfigureWebHost
/// is called lazily on first CreateClient/Server access, by which time PostgresFixture
/// has completed InitializeAsync — so reading the connection string at that point is safe.
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture _postgres;

    public AppFactory(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public const string AdminEmail = "admin@projectja.test";
    public const string AdminPassword = "TestPass123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.ConnectionString);
        builder.UseSetting("ConnectionStrings:TenantDirectory", _postgres.ConnectionString);
        builder.UseSetting("Bootstrap:Enabled", "true");
        builder.UseSetting("Bootstrap:OrgSlug", "default");
        builder.UseSetting("Bootstrap:OrgName", "Test Organization");
        builder.UseSetting("Bootstrap:AdminEmail", AdminEmail);
        builder.UseSetting("Bootstrap:AdminPassword", AdminPassword);
        builder.UseSetting("Tenancy:Mode", "OnPrem");
    }
}

[CollectionDefinition(AppCollection.Name)]
public sealed class AppCollection : ICollectionFixture<PostgresFixture>, ICollectionFixture<AppFactory>
{
    public const string Name = "App";
}

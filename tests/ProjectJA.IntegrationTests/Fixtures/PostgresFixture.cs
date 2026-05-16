// SPDX-License-Identifier: BUSL-1.1
using Testcontainers.PostgreSql;

namespace ProjectJA.IntegrationTests.Fixtures;

/// <summary>
/// Spins up a single Postgres container per test assembly. The connection string
/// is read lazily by <see cref="AppFactory"/> during host build, so this fixture's
/// <see cref="InitializeAsync"/> is guaranteed to have completed before the first
/// test accesses the factory.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("projectja_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}

// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.IntegrationTests.Fixtures;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.IntegrationTests.Tests;

[Collection(AppCollection.Name)]
public sealed class TenantCircuitHandlerTests(AppFactory factory)
{
    // Regression: an unauthenticated Blazor circuit (e.g. /login before sign-in)
    // used to leave ITenantContext unresolved, so any tenant-scoped service
    // resolved on the circuit threw "Tenant has not been resolved for this
    // scope." The handler now falls back to the directory's default tenant in
    // on-prem mode (and host-slug lookup in SaaS).
    [Fact]
    public async Task Unauthenticated_circuit_resolves_default_tenant_in_on_prem_mode()
    {
        // The real ServerAuthenticationStateProvider refuses to run outside a
        // Razor component scope, so swap in an anonymous one for this test.
        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<AuthenticationStateProvider, AnonymousAuthStateProvider>();
            });
        });
        _ = testFactory.CreateClient();

        using var scope = testFactory.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        Assert.False(tenantContext.IsResolved);

        var handler = scope.ServiceProvider.GetRequiredService<CircuitHandler>();
        await handler.OnCircuitOpenedAsync(circuit: null!, CancellationToken.None);

        Assert.True(tenantContext.IsResolved);

        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var defaultTenant = await directory.GetDefaultAsync(default);
        Assert.NotNull(defaultTenant);
        Assert.Equal(defaultTenant!.Id, tenantContext.Current);
    }

    private sealed class AnonymousAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}

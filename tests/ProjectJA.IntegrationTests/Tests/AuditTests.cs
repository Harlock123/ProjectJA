// SPDX-License-Identifier: BUSL-1.1
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.IntegrationTests.Fixtures;
using ProjectJA.Modules.Audit.Contracts;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.IntegrationTests.Tests;

[Collection(AppCollection.Name)]
public sealed class AuditTests(AppFactory factory)
{
    [Fact]
    public async Task Recording_an_event_persists_it_for_query()
    {
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenant = await directory.GetDefaultAsync(default);
        scope.ServiceProvider.UseTenant(tenant!);

        var marker = $"audit-test-{Guid.NewGuid():N}";
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        await audit.RecordAsync(new AuditEntry(
            Action: "test.recorded",
            ResourceType: "Test",
            ResourceId: "x",
            Summary: marker), default);

        var queries = scope.ServiceProvider.GetRequiredService<IAuditQueries>();
        var recent = await queries.ListRecentAsync(50, default);
        Assert.Contains(recent, e => e.Summary == marker);
    }
}

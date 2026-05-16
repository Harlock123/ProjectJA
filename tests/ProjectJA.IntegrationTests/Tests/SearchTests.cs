// SPDX-License-Identifier: BUSL-1.1
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.IntegrationTests.Fixtures;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.IntegrationTests.Tests;

[Collection(AppCollection.Name)]
public sealed class SearchTests(AppFactory factory)
{
    [Fact]
    public async Task Fts_search_finds_issues_by_title_and_description()
    {
        _ = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var directory = scope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenant = await directory.GetDefaultAsync(default);
        scope.ServiceProvider.UseTenant(tenant!);

        var orgs = scope.ServiceProvider.GetRequiredService<IOrganizationQueries>();
        var org = await orgs.GetDefaultAsync(default);
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var key = $"SRCH{Random.Shared.Next(1000, 9999)}";
        var project = Project.Create(org!.Id, key, "Search project", null, clock.UtcNow);
        db.Projects.Add(project);

        var marker = $"capybara{Random.Shared.Next(100000, 999999)}";
        var n1 = project.AllocateIssueNumber();
        db.Issues.Add(Issue.Create(project.Id, n1, $"Find me {marker} in the title", null, Guid.Empty, clock.UtcNow));
        var n2 = project.AllocateIssueNumber();
        db.Issues.Add(Issue.Create(project.Id, n2, "Unrelated", $"description mentions {marker}", Guid.Empty, clock.UtcNow));
        var n3 = project.AllocateIssueNumber();
        db.Issues.Add(Issue.Create(project.Id, n3, "Totally unrelated", "no marker here", Guid.Empty, clock.UtcNow));
        await db.SaveChangesAsync();

        var search = scope.ServiceProvider.GetRequiredService<IIssueSearch>();
        var hits = await search.SearchAsync(marker, 20, default);

        Assert.True(hits.Count >= 2, $"Expected at least 2 hits for '{marker}', got {hits.Count}.");
        Assert.Contains(hits, h => h.Number == n1);
        Assert.Contains(hits, h => h.Number == n2);
        Assert.DoesNotContain(hits, h => h.Number == n3);
    }
}

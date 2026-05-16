// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Infrastructure.Persistence;
using ProjectJA.IntegrationTests.Fixtures;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.IntegrationTests.Tests;

[Collection(AppCollection.Name)]
public sealed class ProjectFlowTests(AppFactory factory)
{
    [Fact]
    public async Task Create_project_and_issue_round_trips_through_db()
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

        var key = $"FLOW{Random.Shared.Next(1000, 9999)}";
        var project = Project.Create(org!.Id, key, "Flow project", null, clock.UtcNow);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var allocated = project.AllocateIssueNumber();
        var issue = Issue.Create(project.Id, allocated, "Flow issue", "body", Guid.Empty, clock.UtcNow);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        var fetched = await db.Issues.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProjectId == project.Id && i.Number == allocated);
        Assert.NotNull(fetched);
        Assert.Equal("Flow issue", fetched!.Title);
        Assert.Equal(IssueStatus.Todo, fetched.Status);

        var projects = scope.ServiceProvider.GetRequiredService<IProjectQueries>();
        var summary = await projects.GetSummaryAsync(project.Id, default);
        Assert.NotNull(summary);
        Assert.Equal(key, summary!.Key);
    }
}

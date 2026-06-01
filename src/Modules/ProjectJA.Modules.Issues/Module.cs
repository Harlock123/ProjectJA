// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Modules.Issues.Application;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Endpoints;
using ProjectJA.Modules.Projects.Contracts;

namespace ProjectJA.Modules.Issues;

public static class IssuesModule
{
    public static IServiceCollection AddIssuesModule(this IServiceCollection services)
    {
        services.AddScoped<IIssueSearch, IssueSearch>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<ISprintIssueOps, SprintIssueOps>();
        services.AddScoped<IIssueLinkService, IssueLinkService>();
        services.AddScoped<IIssueExportService, IssueExportService>();
        services.AddScoped<IIssueImportService, IssueImportService>();
        services.AddScoped<ITagManagementService, TagManagementService>();
        services.AddScoped<IBurndownQueries, BurndownQueries>();
        return services;
    }

    public static IEndpointRouteBuilder MapIssuesEndpoints(this IEndpointRouteBuilder app)
    {
        IssuesEndpoints.Map(app);
        SprintsEndpoints.Map(app);
        return app;
    }
}

// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Modules.Workflows.Application;
using ProjectJA.Modules.Workflows.Contracts;

namespace ProjectJA.Modules.Workflows;

public static class WorkflowsModule
{
    public static IServiceCollection AddWorkflowsModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowQueries, WorkflowQueries>();
        services.AddScoped<IWorkflowSeeder, WorkflowSeeder>();
        return services;
    }

    /// <summary>Workflow REST endpoints live in the Projects module
    /// (`Modules.Projects.Endpoints.WorkflowsEndpoints`) because they need
    /// access to `ProjectAccess` + `IProjectQueries`, and the module reference
    /// runs Projects → Workflows. So this stays a no-op group reservation —
    /// `MapProjectsEndpoints` calls into the real endpoints.</summary>
    public static IEndpointRouteBuilder MapWorkflowsEndpoints(this IEndpointRouteBuilder app) => app;
}

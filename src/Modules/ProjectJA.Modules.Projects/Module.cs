// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Modules.Projects.Application;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Endpoints;

namespace ProjectJA.Modules.Projects;

public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectQueries, ProjectQueries>();
        return services;
    }

    public static IEndpointRouteBuilder MapProjectsEndpoints(this IEndpointRouteBuilder app)
        => ProjectsEndpoints.Map(app);
}

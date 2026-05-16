// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectJA.Modules.Workflows;

public static class WorkflowsModule
{
    public static IServiceCollection AddWorkflowsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapWorkflowsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/workflows");
        return app;
    }
}

// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Modules.Issues.Application;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Endpoints;

namespace ProjectJA.Modules.Issues;

public static class IssuesModule
{
    public static IServiceCollection AddIssuesModule(this IServiceCollection services)
    {
        services.AddScoped<IIssueSearch, IssueSearch>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        return services;
    }

    public static IEndpointRouteBuilder MapIssuesEndpoints(this IEndpointRouteBuilder app)
        => IssuesEndpoints.Map(app);
}

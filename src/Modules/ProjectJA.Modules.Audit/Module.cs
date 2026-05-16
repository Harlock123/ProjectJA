// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Modules.Audit.Application;
using ProjectJA.Modules.Audit.Contracts;
using ProjectJA.SharedKernel.Audit;

namespace ProjectJA.Modules.Audit;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IAuditQueries, AuditQueries>();
        services.AddOptions<AuditOptions>().BindConfiguration("Audit");
        services.AddTransient<AuditRetentionJob>();
        return services;
    }

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/audit");
        return app;
    }
}

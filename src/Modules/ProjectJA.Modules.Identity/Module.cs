// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.Modules.Identity.Application;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Identity.Endpoints;

namespace ProjectJA.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IUserPreferences, UserPreferences>();
        services.AddScoped<IInviteService, InviteService>();
        services.AddScoped<IOidcConfigService, OidcConfigService>();
        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
        => IdentityEndpoints.Map(app);
}

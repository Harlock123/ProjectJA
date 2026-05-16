// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectJA.Modules.Search;

public static class SearchModule
{
    public static IServiceCollection AddSearchModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/search");
        return app;
    }
}

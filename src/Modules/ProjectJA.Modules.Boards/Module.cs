// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectJA.Modules.Boards;

public static class BoardsModule
{
    public static IServiceCollection AddBoardsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapBoardsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/boards");
        return app;
    }
}

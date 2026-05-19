// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ProjectJA.Host.OpenApi;

/// <summary>
/// Declares the API's auth scheme in the OpenAPI document so Scalar surfaces it
/// (lock icons + an auth panel). The API uses ASP.NET Core Identity's
/// application *cookie*, so this is an apiKey-in-cookie scheme — there's no
/// bearer/OAuth flow. Practical flow: sign in at /login in the same browser;
/// Scalar's same-origin "try it" requests then carry the cookie automatically.
/// </summary>
internal sealed class SecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeId = "CookieAuth";
    private const string CookieName = ".AspNetCore.Identity.Application";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = CookieName,
            Description =
                "ASP.NET Core Identity session cookie. There is no token login: " +
                "sign in via the app at /login in this same browser, then the cookie " +
                "is sent automatically with Scalar's same-origin requests.",
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document)] = new List<string>(),
        });

        return Task.CompletedTask;
    }
}

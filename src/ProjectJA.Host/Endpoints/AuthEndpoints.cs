// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using ProjectJA.Host.Authentication;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Host.Endpoints;

internal static class AuthEndpoints
{
    internal static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/signin-external", async (
            HttpContext ctx,
            IAuthenticationSchemeProvider schemeProvider) =>
        {
            // Prefer tenant-specific scheme registered at startup; fall back to the
            // global "oidc" scheme if one is configured.
            string? schemeName = null;
            if (ctx.Items.TryGetValue("Tenant.Slug", out var slugObj) && slugObj is string slug && !string.IsNullOrEmpty(slug))
            {
                var tenantScheme = TenantOidcSchemeRegistrar.SchemePrefix + slug;
                if (await schemeProvider.GetSchemeAsync(tenantScheme) is not null)
                    schemeName = tenantScheme;
            }
            schemeName ??= await schemeProvider.GetSchemeAsync(OidcAuthenticationOptions.Scheme) is not null
                ? OidcAuthenticationOptions.Scheme
                : null;
            if (schemeName is null)
                return Results.Redirect("/login?error=no_oidc");

            var props = new AuthenticationProperties { RedirectUri = "/signin-external/callback" };
            return Results.Challenge(props, [schemeName]);
        });

        app.MapGet("/signin-external/callback", async (
            HttpContext ctx,
            SignInManager<ApplicationUser> signIn,
            UserManager<ApplicationUser> users,
            IOrganizationQueries orgs,
            IOidcConfigService oidcConfig,
            OidcAuthenticationOptions oidcGlobal,
            IClock clock,
            IAuditLog audit) =>
        {
            var info = await signIn.GetExternalLoginInfoAsync();
            if (info is null)
                return Results.Redirect("/login?error=external_failed");

            var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                     ?? info.Principal.FindFirstValue("email");
            if (string.IsNullOrWhiteSpace(email))
                return Results.Redirect("/access-denied?reason=no_email");

            var user = await users.FindByEmailAsync(email);

            if (user is null)
            {
                // Resolve auto-provision setting: tenant-specific config wins, global fallback.
                var tenantCfg = await oidcConfig.GetAsync(ctx.RequestAborted);
                var autoProvision = tenantCfg is not null
                    ? tenantCfg.AutoProvision
                    : oidcGlobal.AutoProvision;
                var allowedDomains = tenantCfg is not null
                    ? tenantCfg.AutoProvisionDomains
                    : (IReadOnlyList<string>)oidcGlobal.AutoProvisionDomains;

                if (autoProvision && DomainAllowed(email, allowedDomains))
                {
                    var org = await orgs.GetDefaultAsync(ctx.RequestAborted);
                    if (org is null)
                        return Results.Redirect("/access-denied?reason=no_org");

                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName)
                                 ?? info.Principal.FindFirstValue("given_name") ?? string.Empty,
                        LastName = info.Principal.FindFirstValue(ClaimTypes.Surname)
                                ?? info.Principal.FindFirstValue("family_name") ?? string.Empty,
                        OrganizationId = org.Id,
                        CreatedAt = clock.UtcNow,
                    };
                    var createResult = await users.CreateAsync(user);
                    if (!createResult.Succeeded)
                        return Results.Redirect("/access-denied?reason=provision_failed");

                    await audit.RecordAsync(new AuditEntry(
                        Action: "user.auto-provisioned",
                        ResourceType: "User",
                        ResourceId: user.Id.ToString(),
                        Summary: $"Auto-provisioned {email} via OIDC ({info.LoginProvider})",
                        Detail: new { provider = info.LoginProvider, domain = email.Split('@', 2)[1] }),
                        ctx.RequestAborted);
                }
                else
                {
                    return Results.Redirect("/access-denied?reason=unknown_user");
                }
            }

            await signIn.SignInAsync(user, isPersistent: false);
            await ctx.SignOutAsync(IdentityConstants.ExternalScheme);

            await audit.RecordAsync(new AuditEntry(
                Action: "oidc.signin",
                ResourceType: "User",
                ResourceId: user.Id.ToString(),
                Summary: $"User {user.Email} signed in via OIDC ({info.LoginProvider})",
                Detail: new { provider = info.LoginProvider }), ctx.RequestAborted);

            return Results.Redirect("/projects");
        });

        return app;
    }

    private static bool DomainAllowed(string email, IReadOnlyList<string> domains)
    {
        if (domains.Count == 0) return false;
        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1) return false;
        var domain = email[(at + 1)..];
        foreach (var allowed in domains)
        {
            if (string.Equals(allowed.TrimStart('@'), domain, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

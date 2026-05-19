// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
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
            IAntiforgery antiforgery,
            IAuthenticationSchemeProvider schemeProvider) =>
        {
            if (await AntiforgeryFailureAsync(antiforgery, ctx) is { } af) return af;

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

        // Sign-out must run in a real HTTP request: an interactive Blazor circuit
        // can't write the auth-cookie deletion to a response that's already been
        // sent. The profile menu POSTs a form here. No [FromForm] params, so the
        // antiforgery middleware doesn't require a token — same as /signin-external.
        app.MapPost("/logout", async (
            HttpContext ctx,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (await AntiforgeryFailureAsync(antiforgery, ctx) is { } af) return af;

            await signIn.SignOutAsync();
            return Results.Redirect("/login");
        });

        // Defensive: a direct GET (bookmark, or the cookie LogoutPath) just
        // bounces to the login page instead of a dead 405.
        app.MapGet("/logout", () => Results.Redirect("/login"));

        // Sign-IN has the same constraint as sign-out. With the whole routed
        // tree InteractiveServer, an EditForm submit runs in the SignalR
        // circuit, where PasswordSignInAsync's cookie write throws
        // "Headers are read-only, response has already started." and the
        // user just sees nothing. The login form POSTs here instead, into a
        // real HTTP request. Path is "/signin" (not "/login") because the
        // routable Login.razor page already owns "/login" and the Razor
        // component endpoint claims POST too — sharing the path throws
        // AmbiguousMatchException. Form read manually (no [FromForm]) so the
        // antiforgery gate matches /signin-external.
        app.MapPost("/signin", async (
            HttpContext ctx,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (await AntiforgeryFailureAsync(antiforgery, ctx) is { } af) return af;

            var form = await ctx.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var password = form["password"].ToString();
            var rememberMe = form["rememberMe"] == "true";
            var returnUrl = form["returnUrl"].ToString();

            var result = await signIn.PasswordSignInAsync(
                email, password, rememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return Results.LocalRedirect(
                    string.IsNullOrWhiteSpace(returnUrl) ? "/projects" : returnUrl);

            var reason = result.IsLockedOut ? "locked"
                       : result.IsNotAllowed ? "notallowed"
                       : "invalid";
            return Results.Redirect($"/login?error={reason}");
        });

        // Accepting an invite creates the user AND signs them in, so it has
        // the same cookie-write-in-circuit problem as /signin. Distinct path
        // ("/accept-invite/submit", not "/accept-invite") to avoid colliding
        // with the routable AcceptInvite.razor page endpoint.
        app.MapPost("/accept-invite/submit", async (
            HttpContext ctx,
            IAntiforgery antiforgery,
            IInviteService invites,
            UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn) =>
        {
            if (await AntiforgeryFailureAsync(antiforgery, ctx) is { } af) return af;

            var form = await ctx.Request.ReadFormAsync();
            var token = form["token"].ToString();
            var firstName = form["firstName"].ToString();
            var lastName = form["lastName"].ToString();
            var password = form["password"].ToString();

            var result = await invites.AcceptAsync(
                new AcceptInviteRequest(token, password, firstName, lastName),
                ctx.RequestAborted);

            if (!result.Success)
            {
                var msg = Uri.EscapeDataString(result.Error ?? "Unable to accept invite.");
                var tok = Uri.EscapeDataString(token);
                return Results.Redirect($"/accept-invite?token={tok}&error={msg}");
            }

            var user = await users.FindByEmailAsync(result.Email!);
            if (user is not null)
                await signIn.SignInAsync(user, isPersistent: false);

            return Results.LocalRedirect("/projects");
        });

        return app;
    }

    // These POSTs read the form manually (no [FromForm]) so the antiforgery
    // middleware doesn't auto-gate them — validate explicitly. On a missing/
    // expired/forged token, bounce to login rather than process the request.
    private static async Task<IResult?> AntiforgeryFailureAsync(IAntiforgery antiforgery, HttpContext ctx)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Redirect("/login?error=expired");
        }
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

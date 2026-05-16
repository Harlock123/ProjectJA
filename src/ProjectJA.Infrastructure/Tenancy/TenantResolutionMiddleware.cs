// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ProjectJA.Infrastructure.Identity;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Tenancy;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _saaSMode;

    public TenantResolutionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _saaSMode = string.Equals(
            configuration["Tenancy:Mode"], "SaaS", StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantDirectory directory)
    {
        TenantInfo? tenant;
        if (_saaSMode)
        {
            var slug = ResolveSlugFromHost(context.Request.Host.Host);
            tenant = slug is null
                ? null
                : await directory.FindBySlugAsync(slug, context.RequestAborted);
        }
        else
        {
            // On-prem: the directory holds exactly one tenant, seeded at startup.
            tenant = await directory.GetDefaultAsync(context.RequestAborted);
        }

        if (tenant is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Tenant not found.");
            return;
        }

        // Mismatch defense (SaaS only): if the user is authenticated under a different
        // tenant than the subdomain resolves to, sign them out and force a fresh login.
        // Also handles legacy cookies that pre-date the tenant_id claim.
        if (_saaSMode && context.User.Identity?.IsAuthenticated == true)
        {
            var claim = context.User.FindFirstValue(TenantStampedClaimsFactory.TenantClaimType);
            var matches = Guid.TryParse(claim, out var claimedTenantGuid)
                       && claimedTenantGuid == tenant.Id.Value;

            if (!matches)
            {
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                if (!context.Response.HasStarted)
                {
                    context.Response.Redirect("/login");
                    return;
                }
            }
        }

        ((TenantContext)tenantContext).Set(tenant);
        context.Items["Tenant.Slug"] = tenant.Slug;

        await _next(context);
    }

    private static string? ResolveSlugFromHost(string host)
    {
        // app.example.com -> "app"; localhost -> null
        if (string.IsNullOrEmpty(host)) return null;
        var firstDot = host.IndexOf('.');
        if (firstDot <= 0) return null;
        var slug = host[..firstDot];
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }
}

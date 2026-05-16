// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Host.Authentication;

/// <summary>
/// At app startup, iterates every tenant in the directory and registers a dedicated
/// OIDC scheme + options for tenants that have a configured/enabled OIDC config.
/// Each scheme has a unique callback path <c>/signin-oidc/{slug}</c>.
///
/// Adding or changing a tenant's OIDC config at runtime requires an app restart for
/// the new scheme to take effect — documented in the README.
/// </summary>
public static class TenantOidcSchemeRegistrar
{
    public const string SchemePrefix = "oidc:";

    public static async Task RegisterAllAsync(IServiceProvider rootServices)
    {
        using var rootScope = rootServices.CreateScope();
        var directory = rootScope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var logger = rootScope.ServiceProvider.GetRequiredService<ILogger<object>>();

        var schemeProvider = rootServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var optionsCache = rootServices.GetRequiredService<IOptionsMonitorCache<OpenIdConnectOptions>>();

        var tenants = await directory.ListAllAsync(CancellationToken.None);
        foreach (var tenant in tenants)
        {
            using var tenantScope = rootServices.CreateScope();
            tenantScope.ServiceProvider.UseTenant(tenant);

            OidcConfigSecret? cfg;
            try
            {
                var service = tenantScope.ServiceProvider.GetRequiredService<IOidcConfigService>();
                cfg = await service.GetForSchemeRegistrationAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load OIDC config for tenant {Tenant}", tenant.Slug);
                continue;
            }

            if (cfg is null || !cfg.Enabled
                || string.IsNullOrWhiteSpace(cfg.Authority)
                || string.IsNullOrWhiteSpace(cfg.ClientId))
            {
                continue;
            }

            var schemeName = SchemePrefix + tenant.Slug;
            if (await schemeProvider.GetSchemeAsync(schemeName) is not null) continue;

            var options = BuildOptions(cfg, tenant.Slug);
            optionsCache.TryAdd(schemeName, options);

            schemeProvider.AddScheme(new AuthenticationScheme(
                schemeName, cfg.DisplayName, typeof(OpenIdConnectHandler)));

            logger.LogInformation("Registered OIDC scheme {Scheme} for tenant {Tenant}", schemeName, tenant.Slug);
        }
    }

    private static OpenIdConnectOptions BuildOptions(OidcConfigSecret cfg, string slug)
    {
        var options = new OpenIdConnectOptions
        {
            SignInScheme = IdentityConstants.ExternalScheme,
            Authority = cfg.Authority,
            ClientId = cfg.ClientId,
            ClientSecret = cfg.ClientSecret,
            ResponseType = OpenIdConnectResponseType.Code,
            UsePkce = true,
            SaveTokens = true,
            GetClaimsFromUserInfoEndpoint = true,
            CallbackPath = $"/signin-oidc/{slug}",
        };
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");
        return options;
    }
}

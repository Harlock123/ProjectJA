// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Host.Authentication;

public sealed class OidcAuthenticationOptions
{
    public const string SectionName = "Authentication:Oidc";
    public const string Scheme = "oidc";

    public bool Enabled { get; set; }

    /// <summary>Label rendered on the login button, e.g. "Sign in with Google".</summary>
    public string DisplayName { get; set; } = "Sign in with SSO";

    /// <summary>OIDC discovery base URL (e.g. https://accounts.google.com).</summary>
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>Additional scopes beyond "openid", "email", "profile".</summary>
    public IList<string> Scopes { get; set; } = new List<string>();

    /// <summary>
    /// When true, OIDC sign-in by an unknown email creates a new <c>ApplicationUser</c>
    /// in the current tenant if the email's domain is in <see cref="AutoProvisionDomains"/>.
    /// Required to be off-by-default for SaaS — without a domain allow-list a random Google
    /// account could sign in to any tenant's subdomain.
    /// </summary>
    public bool AutoProvision { get; set; }

    /// <summary>Email domains allowed to auto-provision (case-insensitive, no leading "@").</summary>
    public IList<string> AutoProvisionDomains { get; set; } = new List<string>();
}

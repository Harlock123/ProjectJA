// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Identity;

namespace ProjectJA.Modules.Identity.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Selected UI theme preset key (see Host ThemeCatalog). Null = app default.</summary>
    public string? ThemeKey { get; set; }

    /// <summary>Whether the user prefers the dark variant of their theme.</summary>
    public bool ThemeDark { get; set; }
}

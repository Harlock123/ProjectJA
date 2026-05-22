// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Identity;

namespace ProjectJA.Modules.Identity.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Tenant-wide administrator flag. Independent of project-level
    /// roles — OrgAdmin gates /audit, /admin/*, /system/versioning, /invites
    /// and the admin-management surface itself. Project Admins still manage
    /// their own projects. Stamped into the auth cookie as the
    /// <c>is_org_admin</c> claim at sign-in, so a change to this field
    /// requires the affected user to sign out + back in for the new policy
    /// gate to apply (same model the <c>tenant_id</c> claim follows).</summary>
    public bool IsOrgAdmin { get; set; }

    /// <summary>Selected UI theme preset key (see Host ThemeCatalog). Null = app default.</summary>
    public string? ThemeKey { get; set; }

    /// <summary>Whether the user prefers the dark variant of their theme.</summary>
    public bool ThemeDark { get; set; }

    /// <summary>Selected avatar preset key (see Host AvatarCatalog). Null = fall back to initials.</summary>
    public string? AvatarKey { get; set; }
}

// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Domain;

/// <summary>A user's role within a single project. Stored as int.
/// Capability semantics (the "clean 3-tier", enforced from Slice 2 on):
/// Viewer = read-only; Member = full issue write (create/edit/move/assign/
/// comment/attachments); Admin = + manage members and project settings.
/// Numeric order is meaningful — higher value ⇒ strictly more capability —
/// so the helpers below compare with >=.</summary>
public enum ProjectRole
{
    Viewer = 0,
    Member = 1,
    Admin = 2
}

public static class ProjectRoleExtensions
{
    /// <summary>May create/edit/move/delete issues, comment, and manage
    /// attachments. Member or Admin.</summary>
    public static bool CanWrite(this ProjectRole role) => role >= ProjectRole.Member;

    /// <summary>May add/remove members and change roles. Admin only.</summary>
    public static bool CanManageMembers(this ProjectRole role) => role >= ProjectRole.Admin;
}

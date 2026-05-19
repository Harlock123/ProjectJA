// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Domain;

/// <summary>A user's role within a single project. Stored as int.
/// Capability semantics (enforced in a later slice):
/// Viewer = read-only; Member = issue CRUD + comments; Admin = + manage
/// members and edit/delete the project.</summary>
public enum ProjectRole
{
    Viewer = 0,
    Member = 1,
    Admin = 2
}

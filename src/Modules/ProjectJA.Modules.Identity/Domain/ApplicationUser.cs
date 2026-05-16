// SPDX-License-Identifier: BUSL-1.1
using Microsoft.AspNetCore.Identity;

namespace ProjectJA.Modules.Identity.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

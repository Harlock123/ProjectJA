// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Contracts;

public sealed record ProjectSummary(Guid Id, Guid OrganizationId, string Key, string Name);

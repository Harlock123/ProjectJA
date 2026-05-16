// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Infrastructure.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string ConnectionString { get; init; } = default!;
    public DateTimeOffset CreatedAt { get; init; }
}

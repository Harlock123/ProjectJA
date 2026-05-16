// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Domain;

public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}

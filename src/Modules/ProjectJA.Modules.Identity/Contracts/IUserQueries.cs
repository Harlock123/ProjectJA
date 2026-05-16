// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Contracts;

public sealed record UserSummary(Guid Id, string Email, string DisplayName);

public interface IUserQueries
{
    Task<UserSummary?> GetByIdAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetSummariesAsync(IEnumerable<Guid> userIds, CancellationToken ct);
}

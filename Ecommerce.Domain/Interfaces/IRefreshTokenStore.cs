namespace Ecommerce.Domain.Interfaces;

public sealed record RefreshTokenEntry(Guid UserId, DateTime ExpiresAt);

public interface IRefreshTokenStore
{
    Task SetAsync(Guid userId, string tokenHash, DateTime expiresAt, CancellationToken ct = default);
    Task<RefreshTokenEntry?> FindByHashAsync(string tokenHash, CancellationToken ct = default);
    Task RemoveAsync(Guid userId, CancellationToken ct = default);
    Task RemoveAllAsync(Guid userId, CancellationToken ct = default);
}

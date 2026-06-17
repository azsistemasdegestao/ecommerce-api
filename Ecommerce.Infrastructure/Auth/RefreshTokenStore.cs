using Ecommerce.Domain.Interfaces;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Auth;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private const string Provider = "EcommerceApi";
    private const string TokenName = "RefreshToken";

    private readonly AppDbContext _context;

    public RefreshTokenStore(AppDbContext context)
    {
        _context = context;
    }

    public async Task SetAsync(Guid userId, string tokenHash, DateTime expiresAt, CancellationToken ct = default)
    {
        var existing = await _context.UserTokens.FirstOrDefaultAsync(
            t => t.UserId == userId && t.LoginProvider == Provider && t.Name == TokenName, ct);

        var value = $"{tokenHash}|{expiresAt.Ticks}";

        if (existing is null)
        {
            _context.UserTokens.Add(new IdentityUserToken<Guid>
            {
                UserId = userId,
                LoginProvider = Provider,
                Name = TokenName,
                Value = value
            });
        }
        else
        {
            existing.Value = value;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<RefreshTokenEntry?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var prefix = tokenHash + "|";

        var token = await _context.UserTokens
            .Where(t => t.LoginProvider == Provider && t.Name == TokenName && t.Value!.StartsWith(prefix))
            .FirstOrDefaultAsync(ct);

        if (token is null)
            return null;

        var ticks = long.Parse(token.Value!.Split('|')[1]);
        return new RefreshTokenEntry(token.UserId, new DateTime(ticks, DateTimeKind.Utc));
    }

    public async Task RemoveAsync(Guid userId, CancellationToken ct = default)
    {
        var token = await _context.UserTokens.FirstOrDefaultAsync(
            t => t.UserId == userId && t.LoginProvider == Provider && t.Name == TokenName, ct);

        if (token is not null)
        {
            _context.UserTokens.Remove(token);
            await _context.SaveChangesAsync(ct);
        }
    }

    public Task RemoveAllAsync(Guid userId, CancellationToken ct = default) => RemoveAsync(userId, ct);
}

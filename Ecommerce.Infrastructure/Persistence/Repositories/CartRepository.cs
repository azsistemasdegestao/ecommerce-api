using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public sealed class CartRepository : ICartRepository
{
    private readonly AppDbContext _context;

    public CartRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _context.Set<Cart>().Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);

    public async Task<Cart?> GetByItemIdAsync(Guid itemId, CancellationToken ct = default) =>
        await _context.Set<Cart>().Include(c => c.Items).FirstOrDefaultAsync(c => c.Items.Any(i => i.Id == itemId), ct);

    public async Task AddAsync(Cart cart, CancellationToken ct = default) =>
        await _context.Set<Cart>().AddAsync(cart, ct);

    public void Update(Cart cart) => _context.Set<Cart>().Update(cart);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct);
}

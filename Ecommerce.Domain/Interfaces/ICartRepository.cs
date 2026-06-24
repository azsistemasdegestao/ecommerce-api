using Ecommerce.Domain.Entities;

namespace Ecommerce.Domain.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Cart?> GetByItemIdAsync(Guid itemId, CancellationToken ct = default);
    Task AddAsync(Cart cart, CancellationToken ct = default);
    void AddItem(CartItem item);
    void Update(Cart cart);
    Task SaveChangesAsync(CancellationToken ct = default);
}

using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Set<Product>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await _context.Set<Product>().FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task AddAsync(Product product, CancellationToken ct = default) =>
        await _context.Set<Product>().AddAsync(product, ct);

    public void Update(Product product) => _context.Set<Product>().Update(product);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct);
}

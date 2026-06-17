using Ecommerce.Domain.Entities;
using Ecommerce.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Persistence.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _context.Set<Category>().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await _context.Set<Category>().FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public async Task<bool> HasActiveProductsAsync(Guid categoryId, CancellationToken ct = default) =>
        await _context.Set<Product>().AnyAsync(p => p.CategoryId == categoryId, ct);

    public async Task AddAsync(Category category, CancellationToken ct = default) =>
        await _context.Set<Category>().AddAsync(category, ct);

    public void Update(Category category) => _context.Set<Category>().Update(category);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct);
}

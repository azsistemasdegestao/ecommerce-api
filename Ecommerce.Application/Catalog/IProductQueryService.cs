using Ecommerce.Application.Common.DTOs;

namespace Ecommerce.Application.Catalog;

public sealed record CategoryRefDto(Guid Id, string Name, string Slug);

public sealed record ProductSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    decimal Price,
    string ImageUrl,
    CategoryRefDto Category,
    bool InStock);

public sealed record ProductDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    decimal Price,
    int Stock,
    string ImageUrl,
    bool InStock,
    CategoryRefDto Category,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CategoryDto(Guid Id, string Name, string Slug, int ProductCount);

public interface IProductQueryService
{
    Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(
        int pageNumber,
        int pageSize,
        string? categorySlug,
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        bool? inStock,
        CancellationToken ct = default);

    Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default);

    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
}

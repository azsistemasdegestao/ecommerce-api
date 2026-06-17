using System.Data;
using Dapper;
using Ecommerce.Application.Catalog;
using Ecommerce.Application.Common.DTOs;

namespace Ecommerce.Infrastructure.Queries;

public sealed class ProductQueryService : IProductQueryService
{
    private readonly IDbConnection _connection;

    public ProductQueryService(IDbConnection connection)
    {
        _connection = connection;
    }

    private sealed record ProductRow(
        Guid Id, string Name, string Slug, decimal Price, string ImageUrl, bool InStock,
        Guid CategoryId, string CategoryName, string CategorySlug, int TotalCount);

    public async Task<PagedResponse<ProductSummaryDto>> GetProductsAsync(
        int pageNumber, int pageSize, string? categorySlug, string? search,
        decimal? minPrice, decimal? maxPrice, bool? inStock, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.id AS "Id", p.name AS "Name", p.slug AS "Slug", p.price AS "Price",
                   p.image_url AS "ImageUrl", p.stock > 0 AS "InStock",
                   c.id AS "CategoryId", c.name AS "CategoryName", c.slug AS "CategorySlug",
                   COUNT(*) OVER()::int AS "TotalCount"
            FROM products p
            JOIN categories c ON c.id = p.category_id
            WHERE p.deleted_at IS NULL AND c.deleted_at IS NULL
              AND (@categorySlug IS NULL OR c.slug = @categorySlug)
              AND (@search IS NULL OR p.name ILIKE '%' || @search || '%')
              AND (@minPrice IS NULL OR p.price >= @minPrice)
              AND (@maxPrice IS NULL OR p.price <= @maxPrice)
              AND (@inStock IS NULL OR (@inStock = true AND p.stock > 0))
            ORDER BY p.created_at DESC
            LIMIT @pageSize OFFSET @offset
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                categorySlug,
                search,
                minPrice,
                maxPrice,
                inStock,
                pageSize,
                offset = (pageNumber - 1) * pageSize
            },
            cancellationToken: ct);

        var rows = (await _connection.QueryAsync<ProductRow>(command)).ToList();

        var items = rows.Select(r => new ProductSummaryDto(
            r.Id, r.Name, r.Slug, r.Price, r.ImageUrl,
            new CategoryRefDto(r.CategoryId, r.CategoryName, r.CategorySlug),
            r.InStock));

        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new PagedResponse<ProductSummaryDto>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<ProductDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.id AS "Id", p.name AS "Name", p.slug AS "Slug", p.description AS "Description",
                   p.price AS "Price", p.stock AS "Stock", p.image_url AS "ImageUrl",
                   p.stock > 0 AS "InStock", p.created_at AS "CreatedAt", p.updated_at AS "UpdatedAt",
                   c.id AS "CategoryId", c.name AS "CategoryName", c.slug AS "CategorySlug"
            FROM products p
            JOIN categories c ON c.id = p.category_id
            WHERE p.slug = @slug AND p.deleted_at IS NULL AND c.deleted_at IS NULL
            """;

        var command = new CommandDefinition(sql, new { slug }, cancellationToken: ct);

        var row = await _connection.QueryFirstOrDefaultAsync<dynamic>(command);
        if (row is null)
            return null;

        return new ProductDetailDto(
            row.Id, row.Name, row.Slug, row.Description, row.Price, row.Stock, row.ImageUrl, row.InStock,
            new CategoryRefDto(row.CategoryId, row.CategoryName, row.CategorySlug),
            row.CreatedAt, row.UpdatedAt);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.id AS "Id", c.name AS "Name", c.slug AS "Slug", COUNT(p.id)::int AS "ProductCount"
            FROM categories c
            LEFT JOIN products p ON p.category_id = c.id AND p.deleted_at IS NULL
            WHERE c.deleted_at IS NULL
            GROUP BY c.id, c.name, c.slug
            ORDER BY c.name
            """;

        var command = new CommandDefinition(sql, cancellationToken: ct);
        var result = await _connection.QueryAsync<CategoryDto>(command);
        return result.ToList();
    }
}

# CONTEXT-catalog.md
> Feature-specific context document for Catalog.

---

## Data Model

```csharp
public sealed class Product : BaseEntity, ISoftDelete
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Slug { get; private set; }
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public string ImageUrl { get; private set; }
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; }
    public DateTime? DeletedAt { get; private set; }
}

public sealed class Category : BaseEntity, ISoftDelete
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public ICollection<Product> Products { get; private set; }
}
```

---

## Cache Keys

```csharp
public static class CacheKeys
{
    public static string Products(string filters) => $"catalog:products:{filters}";
    public static string ProductDetail(string slug) => $"catalog:product:{slug}";
    public static string Categories => "catalog:categories";
}
```

---

## Dapper Queries

```sql
-- GetProductsQuery
SELECT p.id, p.name, p.slug, p.price, p.image_url,
       p.stock > 0 as in_stock,
       c.id as category_id, c.name as category_name, c.slug as category_slug,
       COUNT(*) OVER() as total_count
FROM products p
JOIN categories c ON c.id = p.category_id
WHERE p.deleted_at IS NULL AND c.deleted_at IS NULL
  AND (@CategorySlug IS NULL OR c.slug = @CategorySlug)
  AND (@Search IS NULL OR p.name ILIKE '%' || @Search || '%')
  AND (@MinPrice IS NULL OR p.price >= @MinPrice)
  AND (@MaxPrice IS NULL OR p.price <= @MaxPrice)
  AND (@InStock IS NULL OR (@InStock = true AND p.stock > 0))
ORDER BY p.created_at DESC
LIMIT @PageSize OFFSET @Offset;

-- GetProductBySlugQuery
SELECT p.id, p.name, p.slug, p.description, p.price, p.stock,
       p.image_url, p.stock > 0 as in_stock, p.created_at, p.updated_at,
       c.id as category_id, c.name as category_name, c.slug as category_slug
FROM products p
JOIN categories c ON c.id = p.category_id
WHERE p.slug = @Slug AND p.deleted_at IS NULL;

-- GetCategoriesQuery
SELECT c.id, c.name, c.slug, COUNT(p.id) as product_count
FROM categories c
LEFT JOIN products p ON p.category_id = c.id AND p.deleted_at IS NULL
WHERE c.deleted_at IS NULL
GROUP BY c.id, c.name, c.slug
ORDER BY c.name;
```

---

## File Structure

```
Ecommerce.Domain/
  Entities/Product.cs / Category.cs
  Events/ProductCreated.cs / ProductUpdated.cs / ProductDeleted.cs
  Interfaces/IProductRepository.cs / IProductQueryService.cs

Ecommerce.Application/
  Catalog/
    Commands/CreateProduct/ UpdateProduct/ DeleteProduct/
    Queries/GetProducts/ GetProductBySlug/ GetCategories/

Ecommerce.Infrastructure/
  Persistence/Repositories/ProductRepository.cs
  Persistence/Configurations/ProductConfiguration.cs / CategoryConfiguration.cs
  Queries/ProductQueryService.cs
  Cache/Handlers/ProductUpdatedCacheHandler.cs / ProductDeletedCacheHandler.cs

Ecommerce.API/Endpoints/Catalog/CatalogEndpoints.cs

Ecommerce.UnitTests/Catalog/
  CreateProductHandlerTests.cs / UpdateProductHandlerTests.cs
  DeleteProductHandlerTests.cs / GetProductsHandlerTests.cs
  ProductUpdatedCacheHandlerTests.cs

Ecommerce.IntegrationTests/Catalog/CatalogEndpointsTests.cs
```

---

## References
- [SPEC-catalog.md](./SPEC-catalog.md)
- [GUARDRAILS.md](../../GUARDRAILS.md)
- [ARCHITECTURE.md](../../context/ARCHITECTURE.md)
- [EVENT-PATTERNS.md](../../context/EVENT-PATTERNS.md)
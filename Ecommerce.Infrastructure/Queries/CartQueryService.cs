using System.Data;
using Dapper;
using Ecommerce.Application.Cart;
using Ecommerce.Application.Common.Observability;

namespace Ecommerce.Infrastructure.Queries;

public sealed class CartQueryService : ICartQueryService
{
    private readonly IDbConnection _connection;

    public CartQueryService(IDbConnection connection)
    {
        _connection = connection;
    }

    private sealed record CartRow(
        Guid CartId, DateTime UpdatedAt, Guid? ItemId, Guid? ProductId, int? Quantity, decimal? UnitPrice,
        string? ProductName, string? ProductSlug, string? ImageUrl);

    public async Task<CartDto?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(CartQueryService)}.{nameof(GetByUserIdAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        const string sql = """
            SELECT c.id AS "CartId", c.updated_at AS "UpdatedAt",
                   ci.id AS "ItemId", ci.product_id AS "ProductId", ci.quantity AS "Quantity", ci.unit_price AS "UnitPrice",
                   p.name AS "ProductName", p.slug AS "ProductSlug", p.image_url AS "ImageUrl"
            FROM carts c
            LEFT JOIN cart_items ci ON ci.cart_id = c.id
            LEFT JOIN products p ON p.id = ci.product_id
            WHERE c.user_id = @userId
            """;
        activity?.SetTag("db.statement", sql);

        var command = new CommandDefinition(sql, new { userId }, cancellationToken: ct);
        var rows = (await _connection.QueryAsync<CartRow>(command)).ToList();

        if (rows.Count == 0)
            return null;

        var items = rows
            .Where(r => r.ItemId.HasValue)
            .Select(r => new CartItemDto(
                r.ItemId!.Value, r.ProductId!.Value, r.ProductName!, r.ProductSlug!, r.ImageUrl!,
                r.UnitPrice!.Value, r.Quantity!.Value, r.UnitPrice!.Value * r.Quantity!.Value))
            .ToList();

        return new CartDto(rows[0].CartId, items, items.Sum(i => i.Subtotal), items.Count, rows[0].UpdatedAt);
    }
}

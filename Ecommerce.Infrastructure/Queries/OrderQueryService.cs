using System.Data;
using Dapper;
using Ecommerce.Application.Common.DTOs;
using Ecommerce.Application.Common.Observability;
using Ecommerce.Application.Orders;

namespace Ecommerce.Infrastructure.Queries;

public sealed class OrderQueryService : IOrderQueryService
{
    private readonly IDbConnection _connection;

    public OrderQueryService(IDbConnection connection)
    {
        _connection = connection;
    }

    private sealed record OrderSummaryRow(Guid Id, string Status, decimal Total, DateTime CreatedAt, int ItemCount, int TotalCount);

    public async Task<PagedResponse<OrderSummaryDto>> GetOrdersAsync(
        Guid userId, int pageNumber, int pageSize, string? status, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(OrderQueryService)}.{nameof(GetOrdersAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        const string sql = """
            SELECT o.id AS "Id", o.status AS "Status", o.total AS "Total", o.created_at AS "CreatedAt",
                   COUNT(oi.id)::int AS "ItemCount", COUNT(*) OVER()::int AS "TotalCount"
            FROM orders o
            JOIN order_items oi ON oi.order_id = o.id
            WHERE o.user_id = @userId AND o.deleted_at IS NULL
              AND (@status IS NULL OR o.status = @status)
            GROUP BY o.id, o.status, o.total, o.created_at
            ORDER BY o.created_at DESC
            LIMIT @pageSize OFFSET @offset
            """;
        activity?.SetTag("db.statement", sql);

        var command = new CommandDefinition(
            sql,
            new { userId, status, pageSize, offset = (pageNumber - 1) * pageSize },
            cancellationToken: ct);

        var rows = (await _connection.QueryAsync<OrderSummaryRow>(command)).ToList();

        var items = rows.Select(r => new OrderSummaryDto(r.Id, r.Status, r.Total, r.ItemCount, r.CreatedAt));
        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new PagedResponse<OrderSummaryDto>(items, pageNumber, pageSize, totalCount);
    }

    private sealed record OrderDetailRow(
        Guid OrderId, Guid UserId, string Status, decimal Total, string ShippingAddress,
        DateTime CreatedAt, DateTime UpdatedAt,
        Guid? ItemId, Guid? ProductId, string? ProductName, int? Quantity, decimal? UnitPrice);

    public async Task<OrderDetailDto?> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(OrderQueryService)}.{nameof(GetByIdAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        const string sql = """
            SELECT o.id AS "OrderId", o.user_id AS "UserId", o.status AS "Status", o.total AS "Total",
                   o.shipping_address AS "ShippingAddress", o.created_at AS "CreatedAt", o.updated_at AS "UpdatedAt",
                   oi.id AS "ItemId", oi.product_id AS "ProductId", oi.product_name AS "ProductName",
                   oi.quantity AS "Quantity", oi.unit_price AS "UnitPrice"
            FROM orders o
            LEFT JOIN order_items oi ON oi.order_id = o.id
            WHERE o.id = @orderId AND o.deleted_at IS NULL
            """;
        activity?.SetTag("db.statement", sql);

        var command = new CommandDefinition(sql, new { orderId }, cancellationToken: ct);
        var rows = (await _connection.QueryAsync<OrderDetailRow>(command)).ToList();

        if (rows.Count == 0)
            return null;

        var items = rows
            .Where(r => r.ItemId.HasValue)
            .Select(r => new OrderItemDetailDto(
                r.ItemId!.Value, r.ProductId!.Value, r.ProductName!, r.Quantity!.Value, r.UnitPrice!.Value,
                r.Quantity.Value * r.UnitPrice.Value))
            .ToList();

        var first = rows[0];
        return new OrderDetailDto(
            first.OrderId, first.UserId, first.Status, first.Total, first.ShippingAddress,
            items, first.CreatedAt, first.UpdatedAt);
    }
}

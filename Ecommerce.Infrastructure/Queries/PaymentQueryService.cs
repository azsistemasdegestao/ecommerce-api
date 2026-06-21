using System.Data;
using Dapper;
using Ecommerce.Application.Common.Observability;
using Ecommerce.Application.Payments;

namespace Ecommerce.Infrastructure.Queries;

public sealed class PaymentQueryService : IPaymentQueryService
{
    private readonly IDbConnection _connection;

    public PaymentQueryService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<PaymentDetailDto?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(PaymentQueryService)}.{nameof(GetByOrderIdAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        const string sql = """
            SELECT p.id AS "Id", p.order_id AS "OrderId", o.user_id AS "OrderUserId", p.amount AS "Amount",
                   p.status AS "Status", p.provider AS "Provider", p.created_at AS "CreatedAt", p.updated_at AS "UpdatedAt"
            FROM payments p
            JOIN orders o ON o.id = p.order_id
            WHERE p.order_id = @orderId
            """;
        activity?.SetTag("db.statement", sql);

        var command = new CommandDefinition(sql, new { orderId }, cancellationToken: ct);
        return await _connection.QueryFirstOrDefaultAsync<PaymentDetailDto>(command);
    }
}

using System.Data;
using Dapper;
using Ecommerce.Application.Admin;
using Ecommerce.Application.Common.DTOs;
using Ecommerce.Application.Common.Observability;

namespace Ecommerce.Infrastructure.Queries;

public sealed class AdminQueryService : IAdminQueryService
{
    private readonly IDbConnection _connection;

    public AdminQueryService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<PagedResponse<UserSummaryDto>> GetUsersAsync(
        int pageNumber, int pageSize, string? search, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(AdminQueryService)}.{nameof(GetUsersAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        // Identity tables keep their PascalCase names (e.g. "AspNetUsers") — EFCore.NamingConventions
        // only rewrites conventionally-derived names, not the ones Identity sets explicitly via Fluent API.
        // Their columns, however, are snake_case since those go through the normal convention pipeline.
        const string countSql = """
            SELECT COUNT(*)
            FROM "AspNetUsers" u
            WHERE (@search IS NULL OR u.email ILIKE '%' || @search || '%'
                OR u.first_name ILIKE '%' || @search || '%'
                OR u.last_name ILIKE '%' || @search || '%')
            """;

        const string itemsSql = """
            SELECT
                u.id AS "Id",
                u.first_name AS "FirstName",
                u.last_name AS "LastName",
                u.email AS "Email",
                r.name AS "Role",
                (u.lockout_end IS NOT NULL AND u.lockout_end > now()) AS "IsLocked",
                u.created_at AS "CreatedAt",
                u.deleted_at AS "DeletedAt"
            FROM "AspNetUsers" u
            LEFT JOIN "AspNetUserRoles" ur ON ur.user_id = u.id
            LEFT JOIN "AspNetRoles" r ON r.id = ur.role_id
            WHERE (@search IS NULL OR u.email ILIKE '%' || @search || '%'
                OR u.first_name ILIKE '%' || @search || '%'
                OR u.last_name ILIKE '%' || @search || '%')
            ORDER BY u.created_at DESC
            LIMIT @pageSize OFFSET @offset
            """;
        activity?.SetTag("db.statement", itemsSql);
        activity?.SetTag("db.statement.count", countSql);

        var command = new CommandDefinition(
            countSql,
            new { search },
            cancellationToken: ct);

        var totalCount = await _connection.ExecuteScalarAsync<int>(command);

        var itemsCommand = new CommandDefinition(
            itemsSql,
            new { search, pageSize, offset = (pageNumber - 1) * pageSize },
            cancellationToken: ct);

        var items = await _connection.QueryAsync<UserSummaryDto>(itemsCommand);

        return new PagedResponse<UserSummaryDto>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<UserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(AdminQueryService)}.{nameof(GetUserByIdAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        const string sql = """
            SELECT
                u.id AS "Id",
                u.first_name AS "FirstName",
                u.last_name AS "LastName",
                u.email AS "Email",
                r.name AS "Role",
                (u.lockout_end IS NOT NULL AND u.lockout_end > now()) AS "IsLocked",
                u.lockout_end AS "LockoutEnd",
                u.access_failed_count AS "FailedLoginAttempts",
                u.created_at AS "CreatedAt",
                u.updated_at AS "UpdatedAt",
                u.deleted_at AS "DeletedAt"
            FROM "AspNetUsers" u
            LEFT JOIN "AspNetUserRoles" ur ON ur.user_id = u.id
            LEFT JOIN "AspNetRoles" r ON r.id = ur.role_id
            WHERE u.id = @userId
            """;
        activity?.SetTag("db.statement", sql);

        var command = new CommandDefinition(sql, new { userId }, cancellationToken: ct);
        return await _connection.QueryFirstOrDefaultAsync<UserDetailDto>(command);
    }

    private sealed record AdminOrderRow(
        Guid Id, Guid UserId, string UserEmail, string Status, decimal Total, DateTime CreatedAt, int ItemCount, int TotalCount);

    public async Task<PagedResponse<AdminOrderSummaryDto>> GetOrdersAsync(
        int pageNumber, int pageSize, string? status, Guid? userId, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(AdminQueryService)}.{nameof(GetOrdersAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        const string sql = """
            SELECT o.id AS "Id", o.user_id AS "UserId", u.email AS "UserEmail", o.status AS "Status", o.total AS "Total",
                   o.created_at AS "CreatedAt", COUNT(oi.id)::int AS "ItemCount", COUNT(*) OVER()::int AS "TotalCount"
            FROM orders o
            JOIN order_items oi ON oi.order_id = o.id
            JOIN "AspNetUsers" u ON u.id = o.user_id
            WHERE o.deleted_at IS NULL
              AND (@status IS NULL OR o.status = @status)
              AND (@userId IS NULL OR o.user_id = @userId)
            GROUP BY o.id, o.user_id, u.email, o.status, o.total, o.created_at
            ORDER BY o.created_at DESC
            LIMIT @pageSize OFFSET @offset
            """;
        activity?.SetTag("db.statement", sql);

        var command = new CommandDefinition(
            sql,
            new { status, userId, pageSize, offset = (pageNumber - 1) * pageSize },
            cancellationToken: ct);

        var rows = (await _connection.QueryAsync<AdminOrderRow>(command)).ToList();

        var items = rows.Select(r => new AdminOrderSummaryDto(r.Id, r.UserId, r.UserEmail, r.Status, r.Total, r.ItemCount, r.CreatedAt));
        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new PagedResponse<AdminOrderSummaryDto>(items, pageNumber, pageSize, totalCount);
    }

    private sealed record AdminPaymentRow(
        Guid Id, Guid OrderId, Guid UserId, string UserEmail, decimal Amount, string Status, string PaymentMethod, DateTime CreatedAt, int TotalCount);

    public async Task<PagedResponse<AdminPaymentSummaryDto>> GetPaymentsAsync(
        int pageNumber, int pageSize, CancellationToken ct = default)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(
            $"Dapper {nameof(AdminQueryService)}.{nameof(GetPaymentsAsync)}");
        activity?.SetTag("db.system", "postgresql");
        activity?.SetTag("app.query.type", "dapper");

        const string sql = """
            SELECT p.id AS "Id", p.order_id AS "OrderId", o.user_id AS "UserId", u.email AS "UserEmail",
                   p.amount AS "Amount", p.status AS "Status", p.payment_method AS "PaymentMethod", p.created_at AS "CreatedAt",
                   COUNT(*) OVER()::int AS "TotalCount"
            FROM payments p
            JOIN orders o ON o.id = p.order_id
            JOIN "AspNetUsers" u ON u.id = o.user_id
            ORDER BY p.created_at DESC
            LIMIT @pageSize OFFSET @offset
            """;
        activity?.SetTag("db.statement", sql);

        var command = new CommandDefinition(
            sql, new { pageSize, offset = (pageNumber - 1) * pageSize }, cancellationToken: ct);

        var rows = (await _connection.QueryAsync<AdminPaymentRow>(command)).ToList();

        var items = rows.Select(r => new AdminPaymentSummaryDto(r.Id, r.OrderId, r.UserId, r.UserEmail, r.Amount, r.Status, r.PaymentMethod, r.CreatedAt));
        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;

        return new PagedResponse<AdminPaymentSummaryDto>(items, pageNumber, pageSize, totalCount);
    }
}

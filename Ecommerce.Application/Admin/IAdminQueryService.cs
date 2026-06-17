using Ecommerce.Application.Common.DTOs;

namespace Ecommerce.Application.Admin;

public sealed record UserSummaryDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Role,
    bool IsLocked,
    DateTime CreatedAt,
    DateTime? DeletedAt);

public sealed record UserDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Role,
    bool IsLocked,
    DateTime? LockoutEnd,
    int FailedLoginAttempts,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? DeletedAt);

public sealed record AdminOrderSummaryDto(
    Guid Id, Guid UserId, string UserEmail, string Status, decimal Total, int ItemCount, DateTime CreatedAt);

public sealed record AdminPaymentSummaryDto(
    Guid Id, Guid OrderId, Guid UserId, string UserEmail, decimal Amount, string Status, DateTime CreatedAt);

public interface IAdminQueryService
{
    Task<PagedResponse<UserSummaryDto>> GetUsersAsync(
        int pageNumber, int pageSize, string? search, CancellationToken ct = default);

    Task<UserDetailDto?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);

    Task<PagedResponse<AdminOrderSummaryDto>> GetOrdersAsync(
        int pageNumber, int pageSize, string? status, Guid? userId, CancellationToken ct = default);

    Task<PagedResponse<AdminPaymentSummaryDto>> GetPaymentsAsync(
        int pageNumber, int pageSize, CancellationToken ct = default);
}

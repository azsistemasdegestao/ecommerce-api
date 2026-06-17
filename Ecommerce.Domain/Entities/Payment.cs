using Ecommerce.Domain.Enums;

namespace Ecommerce.Domain.Entities;

public sealed class Payment : BaseEntity
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string Provider { get; private set; } = string.Empty;

    private Payment() { }

    // BR-PAY-003: created Pending
    public static Payment Create(Guid orderId, decimal amount, string provider) => new()
    {
        OrderId = orderId,
        Amount = amount,
        Provider = provider,
        Status = PaymentStatus.Pending
    };

    public void StartProcessing()
    {
        Status = PaymentStatus.Processing;
        UpdateTimestamp();
    }

    public void MarkProcessed()
    {
        Status = PaymentStatus.Processed;
        UpdateTimestamp();
    }

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
        UpdateTimestamp();
    }

    public void Refund()
    {
        Status = PaymentStatus.Refunded;
        UpdateTimestamp();
    }
}

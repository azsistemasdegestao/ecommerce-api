namespace Ecommerce.Domain.Interfaces;

public interface ISoftDelete
{
    DateTime? DeletedAt { get; }
    void SoftDelete();
}

using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Domain.Entities;

public sealed class Category : BaseEntity, ISoftDelete
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public DateTime? DeletedAt { get; private set; }

    private Category() { }

    public static Category Create(string name, string slug) => new() { Name = name, Slug = slug };

    public void Update(string name, string slug)
    {
        Name = name;
        Slug = slug;
        UpdateTimestamp();
    }

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;
}

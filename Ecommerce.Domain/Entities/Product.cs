using Ecommerce.Domain.Interfaces;

namespace Ecommerce.Domain.Entities;

public sealed class Product : BaseEntity, ISoftDelete
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private Product() { }

    public static Product Create(
        string name, string description, string slug, decimal price, int stock, string imageUrl, Guid categoryId)
    {
        return new Product
        {
            Name = name,
            Description = description,
            Slug = slug,
            Price = price,
            Stock = stock,
            ImageUrl = imageUrl,
            CategoryId = categoryId
        };
    }

    public void Update(string name, string description, decimal price, int stock, string imageUrl)
    {
        Name = name;
        Description = description;
        Price = price;
        Stock = stock;
        ImageUrl = imageUrl;
        UpdateTimestamp();
    }

    public void UpdateImage(string imageUrl)
    {
        ImageUrl = imageUrl;
        UpdateTimestamp();
    }

    public void SoftDelete() => DeletedAt = DateTime.UtcNow;
}

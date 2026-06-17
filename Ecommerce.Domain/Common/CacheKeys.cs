namespace Ecommerce.Domain.Common;

public static class CacheKeys
{
    public static string ProductList(string filters) => $"catalog:products:{filters}";
    public static string ProductDetail(string slug) => $"catalog:product:{slug}";
    public static string Categories => "catalog:categories";
}

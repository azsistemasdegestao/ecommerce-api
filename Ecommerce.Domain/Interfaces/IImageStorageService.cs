namespace Ecommerce.Domain.Interfaces;

public interface IImageStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string imageUrl, CancellationToken ct = default);
}

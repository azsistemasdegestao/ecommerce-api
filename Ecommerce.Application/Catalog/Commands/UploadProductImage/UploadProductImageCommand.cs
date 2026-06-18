using MediatR;

namespace Ecommerce.Application.Catalog.Commands.UploadProductImage;

public sealed record UploadProductImageCommand(
    Guid ProductId,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSize) : IRequest<UploadProductImageResponse>;

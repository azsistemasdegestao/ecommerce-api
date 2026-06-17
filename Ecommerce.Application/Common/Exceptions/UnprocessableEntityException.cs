namespace Ecommerce.Application.Common.Exceptions;

public sealed class UnprocessableEntityException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public UnprocessableEntityException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }
}

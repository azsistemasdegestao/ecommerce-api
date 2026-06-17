namespace Ecommerce.Application.Common.Exceptions;

public sealed class AccountLockedException : Exception
{
    public AccountLockedException(string message) : base(message) { }
}

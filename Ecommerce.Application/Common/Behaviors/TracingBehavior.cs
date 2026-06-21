using Ecommerce.Application.Common.Observability;
using MediatR;

namespace Ecommerce.Application.Common.Behaviors;

public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var activity = ApplicationActivitySource.Instance.StartActivity(typeof(TRequest).Name);

        return await next(cancellationToken);
    }
}

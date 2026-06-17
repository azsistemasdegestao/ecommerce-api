using Ecommerce.Application.Common.Behaviors;
using Ecommerce.Application.Payments.EventHandlers;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ecommerce.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationExtensions).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<IEventHandler<PaymentRequested>, PaymentRequestedHandler>();
        services.AddScoped<IEventHandler<PaymentProcessed>, PaymentProcessedHandler>();
        services.AddScoped<IEventHandler<PaymentFailed>, PaymentFailedHandler>();
        services.AddScoped<IEventHandler<PaymentRefunded>, PaymentRefundedHandler>();

        return services;
    }
}

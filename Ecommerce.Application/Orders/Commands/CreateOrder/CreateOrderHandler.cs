using Ecommerce.Application.Common.Exceptions;
using Ecommerce.Domain.Events;
using Ecommerce.Domain.Interfaces;
using MediatR;
using DomainOrder = Ecommerce.Domain.Entities.Order;

namespace Ecommerce.Application.Orders.Commands.CreateOrder;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IEventBus _eventBus;

    public CreateOrderHandler(
        ICartRepository cartRepository,
        IProductRepository productRepository,
        IOrderRepository orderRepository,
        IEventBus eventBus)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
        _orderRepository = orderRepository;
        _eventBus = eventBus;
    }

    public async Task<CreateOrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        // BR-ORD-001
        if (cart is null || cart.Items.Count == 0)
            throw new UnprocessableEntityException("Cart is empty.");

        var orderItems = new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>();
        foreach (var cartItem in cart.Items)
        {
            var product = await _productRepository.GetByIdAsync(cartItem.ProductId, cancellationToken)
                ?? throw new UnprocessableEntityException("A product in the cart no longer exists.");

            // BR-ORD-002
            if (product.Stock < cartItem.Quantity)
                throw new UnprocessableEntityException("Insufficient stock for one or more items.");

            // BR-ORD-005: snapshot name/price at Checkout time, not the Cart's original snapshot
            orderItems.Add((product.Id, product.Name, cartItem.Quantity, product.Price));
        }

        var order = DomainOrder.Create(request.UserId, request.ShippingAddress, orderItems);
        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        // BR-ORD-004
        cart.Clear();
        _cartRepository.Update(cart);
        await _cartRepository.SaveChangesAsync(cancellationToken);

        // BR-ORD-008
        await _eventBus.PublishAsync(new OrderCreated(
            EventId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            OrderId: order.Id,
            UserId: order.UserId,
            Total: order.Total), cancellationToken);

        return new CreateOrderResponse(order.Id, order.Status.ToString(), order.Total, order.ShippingAddress, order.Items.Count, order.CreatedAt);
    }
}

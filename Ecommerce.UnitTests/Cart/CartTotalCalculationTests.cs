using Ecommerce.Domain.Entities;
using FluentAssertions;
using Xunit;
using DomainCart = Ecommerce.Domain.Entities.Cart;

namespace Ecommerce.UnitTests.Cart;

public class CartTotalCalculationTests
{
    // AC-CART-U10
    [Fact]
    public void Should_Calculate_Total_As_Sum_Of_Subtotals_When_Items_Have_Different_Prices()
    {
        // Arrange
        var cart = DomainCart.Create(Guid.NewGuid());
        var productA = Product.Create("A", "desc", "a", 10m, 10, "url", Guid.NewGuid());
        var productB = Product.Create("B", "desc", "b", 25.50m, 10, "url", Guid.NewGuid());

        // Act
        cart.AddItem(productA.Id, 2, productA.Price);
        cart.AddItem(productB.Id, 3, productB.Price);

        // Assert
        cart.Total.Should().Be(2 * 10m + 3 * 25.50m);
        cart.ItemCount.Should().Be(2);
    }
}

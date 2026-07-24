using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Tests;

public class OrderServiceCancelTests
{
    private static async Task<Order> CreateOrderWithStatusAsync(
        Core.Services.OrderService service,
        Infrastructure.Data.OrderHubDbContext db,
        OrderStatus status)
    {
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db);
        var result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });
        var order = result.Value!;
        order.Status = status;
        await db.SaveChangesAsync();
        return order;
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task CancelOrder_ActiveOrder_SetsStatusCancelled(OrderStatus initialStatus)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var order = await CreateOrderWithStatusAsync(service, db, initialStatus);

        var result = await service.CancelOrderAsync(order.Id);

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.Cancelled, db.Orders.Single(o => o.Id == order.Id).Status);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task CancelOrder_NotCancellableStatus_Fails(OrderStatus initialStatus)
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var order = await CreateOrderWithStatusAsync(service, db, initialStatus);

        var stockBefore = db.Products.Single(p => p.Id == order.Items.Single().ProductId).StockQuantity;

        var result = await service.CancelOrderAsync(order.Id);

        Assert.False(result.Success);
        Assert.Equal(initialStatus, db.Orders.Single(o => o.Id == order.Id).Status);
        // 不可取消的訂單不得誤回補庫存：庫存應維持取消嘗試前的值。
        Assert.Equal(stockBefore, db.Products.Single(p => p.Id == order.Items.Single().ProductId).StockQuantity);
    }

    [Fact]
    public async Task CancelOrder_RestoresProductStock()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 10);

        var created = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 3) });
        Assert.True(created.Success);
        // 建單後庫存先扣掉 3 → 7。
        Assert.Equal(7, db.Products.Single(p => p.Id == product.Id).StockQuantity);

        var result = await service.CancelOrderAsync(created.Value!.Id);

        Assert.True(result.Success);
        // 取消後庫存應回補為原值 10（修復前回補迴圈不執行，庫存會停在 7）。
        Assert.Equal(10, db.Products.Single(p => p.Id == product.Id).StockQuantity);
    }

    [Fact]
    public async Task CancelOrder_NotFound_Fails()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderService(db);

        var result = await service.CancelOrderAsync(12345);

        Assert.False(result.Success);
        Assert.Contains("找不到", result.ErrorMessage);
    }
}

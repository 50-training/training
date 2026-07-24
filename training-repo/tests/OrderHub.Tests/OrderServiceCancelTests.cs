using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Tests;

public class OrderServiceCancelTests
{
    private static async Task<Order> CreateOrderWithStatusAsync(
        OrderService service,
        OrderHubDbContext db,
        OrderStatus status)
    {
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db);
        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });
        Order order = result.Value!;
        order.Status = status;
        await db.SaveChangesAsync();
        return order;
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task CancelOrder_ActiveOrder_SetsStatusCancelled(OrderStatus initialStatus)
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Order order = await CreateOrderWithStatusAsync(service, db, initialStatus);

        ServiceResult<Order> result = await service.CancelOrderAsync(order.Id);

        Assert.True(result.Success);
        Assert.Equal(OrderStatus.Cancelled, db.Orders.Single(o => o.Id == order.Id).Status);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task CancelOrder_NotCancellableStatus_Fails(OrderStatus initialStatus)
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Order order = await CreateOrderWithStatusAsync(service, db, initialStatus);

        int stockBefore = db.Products.Single(p => p.Id == order.Items.Single().ProductId).StockQuantity;

        ServiceResult<Order> result = await service.CancelOrderAsync(order.Id);

        Assert.False(result.Success);
        Assert.Equal(initialStatus, db.Orders.Single(o => o.Id == order.Id).Status);
        // 不可取消的訂單不得誤回補庫存：庫存應維持取消嘗試前的值。
        Assert.Equal(stockBefore, db.Products.Single(p => p.Id == order.Items.Single().ProductId).StockQuantity);
    }

    [Fact]
    public async Task CancelOrder_RestoresProductStock()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db, stock: 10);

        ServiceResult<Order> created = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 3) });
        Assert.True(created.Success);
        // 建單後庫存先扣掉 3 → 7。
        Assert.Equal(7, db.Products.Single(p => p.Id == product.Id).StockQuantity);

        ServiceResult<Order> result = await service.CancelOrderAsync(created.Value!.Id);

        Assert.True(result.Success);
        // 取消後庫存應回補為原值 10（修復前回補迴圈不執行，庫存會停在 7）。
        Assert.Equal(10, db.Products.Single(p => p.Id == product.Id).StockQuantity);
    }

    [Fact]
    public async Task CancelOrder_NotFound_Fails()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);

        ServiceResult<Order> result = await service.CancelOrderAsync(12345);

        Assert.False(result.Success);
        Assert.Contains("找不到", result.ErrorMessage);
    }
}

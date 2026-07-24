using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Tests;

public class OrderServiceCreateTests
{
    [Fact]
    public async Task CreateOrder_HappyPath_CreatesPendingOrder()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 2) });

        Assert.True(result.Success);
        Assert.NotNull(result.Value);
        Assert.Equal(OrderStatus.Pending, result.Value!.Status);
        Assert.Single(result.Value.Items);
        Assert.Equal(1, db.Orders.Count());
    }

    [Fact]
    public async Task CreateOrder_SnapshotsCurrentUnitPrice()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db, unitPrice: 380m);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });

        Assert.True(result.Success);
        Assert.Equal(380m, result.Value!.Items.Single().UnitPriceSnapshot);
    }

    [Fact]
    public async Task CreateOrder_GoldCustomer_SnapshotsRawUnitPrice_AndDiscountsOnce()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db, tier: CustomerTier.Gold);
        Product product = TestSetup.AddProduct(db, unitPrice: 1000m);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });

        Assert.True(result.Success);

        // 單價快照應存原價，不可在建單時先為金卡打折（修復前會是 900）。
        Assert.Equal(1000m, result.Value!.Items.Single().UnitPriceSnapshot);

        // 明細頁會帶入 Customer 導覽屬性（GetWithDetailsAsync 有 Include Customer）；
        // 金卡折扣只應在計算總額時套一次：1000 × (1 - 0.10) = 900（修復前雙重折扣為 810）。
        result.Value.Customer = customer;
        Assert.Equal(900m, service.CalculateTotal(result.Value));
    }

    [Fact]
    public async Task CreateOrder_DecrementsProductStock()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db, stock: 10);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 3) });

        Assert.True(result.Success);
        Assert.Equal(7, db.Products.Single(p => p.Id == product.Id).StockQuantity);
    }

    [Fact]
    public async Task CreateOrder_UnknownCustomer_Fails()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Product product = TestSetup.AddProduct(db);

        ServiceResult<Order> result = await service.CreateOrderAsync(999, new[] { new NewOrderLine(product.Id, 1) });

        Assert.False(result.Success);
        Assert.Contains("客戶", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateOrder_EmptyLines_Fails()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, Array.Empty<NewOrderLine>());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_NonPositiveQuantity_Fails()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 0) });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_DuplicateProduct_Fails()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[]
        {
            new NewOrderLine(product.Id, 1),
            new NewOrderLine(product.Id, 2)
        });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_InactiveProduct_Fails()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db, isActive: false);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 1) });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_FailsWithMessage()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db, stock: 2);

        ServiceResult<Order> result = await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 5) });

        Assert.False(result.Success);
        Assert.Contains("庫存不足", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateOrder_Failed_DoesNotPersistOrder()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db, stock: 2);

        await service.CreateOrderAsync(customer.Id, new[] { new NewOrderLine(product.Id, 5) });

        Assert.Equal(0, db.Orders.Count());
    }
}

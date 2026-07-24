using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Tests;

public class ProductServiceLowStockTests
{
    [Fact]
    public async Task GetLowStock_FiltersByThreshold_AndSortsByStockAscending()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        ProductService service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 8, sku: "SKU-LO8");
        TestSetup.AddProduct(db, stock: 2, sku: "SKU-LO2");
        TestSetup.AddProduct(db, stock: 10, sku: "SKU-EQ10"); // 邊界：等於門檻不納入
        TestSetup.AddProduct(db, stock: 15, sku: "SKU-HI15");

        IReadOnlyList<LowStockItem> result = await service.GetLowStockAsync(10);

        // 只保留 < 10 的兩筆，且依庫存升冪。
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "SKU-LO2", "SKU-LO8" }, result.Select(r => r.Sku));
        // 無任何訂單時，近 30 天售出數量應補 0。
        Assert.All(result, r => Assert.Equal(0, r.UnitsSoldLast30Days));
    }

    [Fact]
    public async Task GetLowStock_SameStock_OrdersByUnitsSoldDescending()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        ProductService service = TestSetup.CreateProductService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product productA = TestSetup.AddProduct(db, stock: 3, sku: "SKU-A"); // 售出 5
        Product productB = TestSetup.AddProduct(db, stock: 3, sku: "SKU-B"); // 售出 10
        DateTime now = DateTime.UtcNow;

        db.Orders.AddRange(
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Shipped,
                CreatedAt = now.AddDays(-3),
                Items = { new OrderItem { ProductId = productA.Id, Quantity = 5, UnitPriceSnapshot = 100m } }
            },
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Shipped,
                CreatedAt = now.AddDays(-3),
                Items = { new OrderItem { ProductId = productB.Id, Quantity = 10, UnitPriceSnapshot = 100m } }
            });
        db.SaveChanges();

        IReadOnlyList<LowStockItem> result = await service.GetLowStockAsync(10);

        // 庫存相同（皆 3），依近 30 天售出逆序：B(10) 在 A(5) 之前。
        Assert.Equal(new[] { "SKU-B", "SKU-A" }, result.Select(r => r.Sku));
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        ProductService service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, stock: 3, isActive: true, sku: "SKU-ACT");
        // 停售且庫存更低：若被誤納會排在最前面。
        TestSetup.AddProduct(db, stock: 1, isActive: false, sku: "SKU-INACT");

        IReadOnlyList<LowStockItem> result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal("SKU-ACT", result[0].Sku);
    }

    [Fact]
    public async Task GetLowStock_UnitsSold_ExcludesCancelledAndOlderThan30Days()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        ProductService service = TestSetup.CreateProductService(db);
        Customer customer = TestSetup.AddCustomer(db);
        Product product = TestSetup.AddProduct(db, stock: 3, sku: "SKU-P");
        DateTime now = DateTime.UtcNow;

        db.Orders.AddRange(
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Shipped,
                CreatedAt = now.AddDays(-5),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 4, UnitPriceSnapshot = 100m } }
            },
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Confirmed,
                CreatedAt = now.AddDays(-10),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 3, UnitPriceSnapshot = 100m } }
            },
            new Order // 已取消：排除
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Cancelled,
                CreatedAt = now.AddDays(-2),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 100, UnitPriceSnapshot = 100m } }
            },
            new Order // 超過 30 天：排除
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Shipped,
                CreatedAt = now.AddDays(-40),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 50, UnitPriceSnapshot = 100m } }
            });
        db.SaveChanges();

        IReadOnlyList<LowStockItem> result = await service.GetLowStockAsync(10);

        // 只計入近 30 天且非 Cancelled：4 + 3 = 7（漏排 Cancelled 會變 107、漏排舊單變 57）。
        LowStockItem row = Assert.Single(result);
        Assert.Equal(7, row.UnitsSoldLast30Days);
    }
}

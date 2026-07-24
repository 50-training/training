using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Tests;

public class OrderServiceQueryTests
{
    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);

        db.Orders.AddRange(
            new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        PagedResult<Order> result = await service.GetOrdersAsync(1, 20, OrderStatus.Shipped);

        Assert.All(result.Items, o => Assert.Equal(OrderStatus.Shipped, o.Status));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task GetOrders_ReportsTotalCountAndTotalPages()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);

        for (int i = 0; i < 45; i++)
        {
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = DateTime.UtcNow.AddMinutes(-i) });
        }
        db.SaveChanges();

        PagedResult<Order> result = await service.GetOrdersAsync(1, 20, null);

        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GetOrders_FirstPage_IncludesNewestOrder_AndReturnsFullPage()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);

        DateTime baseTime = DateTime.UtcNow;
        // i=0 為最新（CreatedAt 最大），依 CreatedAt 由新到舊排序後應排在第一頁最前面。
        for (int i = 0; i < 25; i++)
        {
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = baseTime.AddMinutes(-i) });
        }
        db.SaveChanges();

        PagedResult<Order> result = await service.GetOrdersAsync(1, 20, null);

        // 第 1 頁應回傳滿頁 20 筆，且包含最新那筆（修復前 Skip(page*pageSize) 會跳過最新 20 筆，只回 5 筆）。
        Assert.Equal(20, result.Items.Count);
        Assert.Equal(baseTime, result.Items[0].CreatedAt);
    }

    [Fact]
    public async Task GetOrders_LastPage_ReturnsRemainingItems_NotEmpty()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customer = TestSetup.AddCustomer(db);

        DateTime baseTime = DateTime.UtcNow;
        for (int i = 0; i < 25; i++)
        {
            db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Confirmed, CreatedAt = baseTime.AddMinutes(-i) });
        }
        db.SaveChanges();

        // 25 筆、每頁 20 → 共 2 頁；最後一頁應有剩下 5 筆（修復前 Skip(2*20=40) 會超過總筆數而回傳空頁）。
        PagedResult<Order> result = await service.GetOrdersAsync(2, 20, null);

        Assert.Equal(2, result.TotalPages);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsOnlyThatCustomersOrders()
    {
        using OrderHubDbContext db = TestSetup.CreateContext();
        OrderService service = TestSetup.CreateOrderService(db);
        Customer customerA = TestSetup.AddCustomer(db, name: "客戶A");
        Customer customerB = TestSetup.AddCustomer(db, name: "客戶B");

        db.Orders.AddRange(
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerB.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = customerA.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        IReadOnlyList<Order> orders = await service.GetCustomerOrdersAsync(customerA.Id);

        Assert.Equal(2, orders.Count);
        Assert.All(orders, o => Assert.Equal(customerA.Id, o.CustomerId));
    }
}

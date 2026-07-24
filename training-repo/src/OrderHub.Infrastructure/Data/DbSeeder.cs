using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Domain;

namespace OrderHub.Infrastructure.Data;

/// <summary>
/// 開發環境種子資料。使用固定 random seed，確保每次重建資料庫的內容一致。
/// </summary>
public static class DbSeeder
{
    private const int RandomSeed = 20260101;

    private static readonly string[] CustomerNames =
    {
        "陳志明", "林淑芬", "黃冠宇", "張雅婷", "李建宏",
        "王美玲", "吳宗翰", "劉思穎", "蔡承翰", "楊佩珊",
        "許家豪", "鄭欣怡", "謝博文", "洪千惠", "郭俊傑",
        "曾雅雯", "邱柏睿", "賴怡君", "周振宇", "徐若瑄"
    };

    private static readonly string[] CustomerEmails =
    {
        "chen.zhiming", "lin.shufen", "huang.guanyu", "zhang.yating", "li.jianhong",
        "wang.meiling", "wu.zonghan", "liu.siying", "tsai.chenghan", "yang.peishan",
        "hsu.chiahao", "cheng.hsinyi", "hsieh.powen", "hung.chienhui", "kuo.chunchieh",
        "tseng.yawen", "chiu.porui", "lai.yichun", "chou.chenyu", "hsu.johsuan"
    };

    private static readonly int[] GoldIndexes = { 0, 7, 14 };
    private static readonly int[] SilverIndexes = { 2, 5, 9, 12, 18 };

    private static readonly string[] ProductSeries = { "極光", "星河", "雲峰", "曜石", "晨光" };

    private static readonly string[] ProductBaseNames =
    {
        "無線滑鼠", "機械鍵盤", "27吋螢幕", "USB-C 集線器", "筆電支架",
        "網路攝影機", "降噪耳機", "行動電源", "HDMI 傳輸線", "桌上麥克風"
    };

    public static async Task SeedAsync(OrderHubDbContext db)
    {
        if (await db.Customers.AnyAsync())
        {
            return;
        }

        Random random = new Random(RandomSeed);
        DateTime now = DateTime.UtcNow;

        // --- Customers: 20 位（12 Standard / 5 Silver / 3 Gold） ---
        List<Customer> customers = new List<Customer>();
        for (int i = 0; i < CustomerNames.Length; i++)
        {
            CustomerTier tier = GoldIndexes.Contains(i) ? CustomerTier.Gold
                : SilverIndexes.Contains(i) ? CustomerTier.Silver
                : CustomerTier.Standard;

            customers.Add(new Customer
            {
                Name = CustomerNames[i],
                Email = $"{CustomerEmails[i]}@example.com.tw",
                Tier = tier,
                CreatedAt = now.AddDays(-random.Next(120, 720))
            });
        }
        db.Customers.AddRange(customers);

        // --- Products: 50 個，其中 5 個低庫存（< 10） ---
        List<Product> products = new List<Product>();
        int skuNumber = 1001;
        foreach (string series in ProductSeries)
        {
            foreach (string baseName in ProductBaseNames)
            {
                products.Add(new Product
                {
                    Sku = $"SKU-{skuNumber++}",
                    Name = $"{series} {baseName}",
                    UnitPrice = random.Next(9, 400) * 10m,
                    StockQuantity = random.Next(15, 120),
                    IsActive = true
                });
            }
        }

        // 指定 5 個商品為低庫存、3 個商品停售（低庫存與停售不重疊）
        int[] lowStockIndexes = { 4, 13, 22, 31, 47 };
        foreach (int idx in lowStockIndexes)
        {
            products[idx].StockQuantity = random.Next(2, 10);
        }

        int[] inactiveIndexes = { 8, 26, 40 };
        foreach (int idx in inactiveIndexes)
        {
            products[idx].IsActive = false;
        }

        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // --- Orders: 200 筆，近 90 天，各狀態都有 ---
        List<Order> orders = new List<Order>();
        for (int i = 0; i < 200; i++)
        {
            Customer customer = customers[random.Next(customers.Count)];
            Order order = new Order
            {
                CustomerId = customer.Id,
                Status = PickStatus(random),
                CreatedAt = now.AddMinutes(-random.Next(30, 90 * 24 * 60))
            };

            int lineCount = random.Next(1, 5);
            HashSet<int> pickedProductIndexes = new HashSet<int>();
            for (int j = 0; j < lineCount; j++)
            {
                int productIndex;
                do
                {
                    productIndex = random.Next(products.Count);
                } while (!pickedProductIndexes.Add(productIndex));

                Product product = products[productIndex];
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = random.Next(1, 6),
                    UnitPriceSnapshot = product.UnitPrice
                });
            }

            orders.Add(order);
        }

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
    }

    private static OrderStatus PickStatus(Random random) => random.Next(100) switch
    {
        < 15 => OrderStatus.Pending,
        < 40 => OrderStatus.Confirmed,
        < 85 => OrderStatus.Shipped,
        _ => OrderStatus.Cancelled
    };
}

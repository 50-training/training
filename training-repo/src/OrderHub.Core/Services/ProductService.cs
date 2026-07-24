using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;

    public ProductService(IProductRepository productRepository, IOrderRepository orderRepository)
    {
        _productRepository = productRepository;
        _orderRepository = orderRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(int threshold)
    {
        var products = await _productRepository.GetLowStockAsync(threshold);
        if (products.Count == 0)
            return Array.Empty<LowStockItem>();

        var since = DateTime.UtcNow.AddDays(-30);
        var ids = products.Select(p => p.Id).ToList();
        var sales = await _orderRepository.GetUnitsSoldByProductSinceAsync(since, ids);
        var soldById = sales.ToDictionary(s => s.ProductId, s => s.UnitsSold);

        // 庫存升冪；庫存相同時再依近 30 天售出逆序（售得多的排前面）。
        // 次要排序需在合併出售出數量後才能套用，故排序在 service 而非 repository。
        return products
            .Select(p => new LowStockItem(p.Id, p.Sku, p.Name, p.StockQuantity,
                soldById.TryGetValue(p.Id, out var q) ? q : 0))
            .OrderBy(i => i.StockQuantity)
            .ThenByDescending(i => i.UnitsSoldLast30Days)
            .ToList();
    }
}

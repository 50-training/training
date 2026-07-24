namespace OrderHub.Core.Services;

public record LowStockItem(int ProductId, string Sku, string Name, int StockQuantity, int UnitsSoldLast30Days);

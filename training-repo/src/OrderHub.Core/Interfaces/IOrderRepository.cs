using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Core.Interfaces;

public interface IOrderRepository
{
    Task<PagedResult<Order>> GetPagedAsync(int page, int pageSize, OrderStatus? status);
    Task<Order?> GetWithDetailsAsync(int id);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(int customerId);
    Task<IReadOnlyList<ProductSales>> GetUnitsSoldByProductSinceAsync(DateTime since, IReadOnlyCollection<int> productIds);
    Task AddAsync(Order order);
    Task SaveChangesAsync();
}

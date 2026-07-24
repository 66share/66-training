using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly OrderHubDbContext _db;

    public ProductRepository(OrderHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.OrderBy(p => p.Sku).ToListAsync();

    public async Task<IReadOnlyList<Product>> GetActiveAsync() =>
        await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Sku).ToListAsync();

    public Task<Product?> GetByIdAsync(int id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<ProductLowStockInfo>> GetLowStockAsync(int threshold, DateTime salesSinceUtc)
    {
        var products = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity < threshold)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();

        if (products.Count == 0)
            return Array.Empty<ProductLowStockInfo>();

        var productIds = products.Select(p => p.Id).ToList();

        var salesByProduct = await (
            from oi in _db.OrderItems
            join o in _db.Orders on oi.OrderId equals o.Id
            where productIds.Contains(oi.ProductId)
                && o.Status != OrderStatus.Cancelled
                && o.CreatedAt >= salesSinceUtc
            group oi by oi.ProductId into g
            select new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) }
        ).ToDictionaryAsync(x => x.ProductId, x => x.Sold);

        return products
            .Select(p => new ProductLowStockInfo(p, salesByProduct.TryGetValue(p.Id, out var sold) ? sold : 0))
            .ToList();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}

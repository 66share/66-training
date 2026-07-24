using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;

namespace OrderHub.Core.Services;

public class ProductService : IProductService
{
    private const int SalesWindowDays = 30;

    private readonly IProductRepository _productRepository;

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public Task<IReadOnlyList<Product>> GetAllAsync() => _productRepository.GetAllAsync();

    public Task<IReadOnlyList<Product>> GetActiveAsync() => _productRepository.GetActiveAsync();

    public Task<IReadOnlyList<ProductLowStockInfo>> GetLowStockAsync(int threshold)
    {
        if (threshold < 1) threshold = 1;
        var cutoffUtc = DateTime.UtcNow.AddDays(-SalesWindowDays);
        return _productRepository.GetLowStockAsync(threshold, cutoffUtc);
    }
}

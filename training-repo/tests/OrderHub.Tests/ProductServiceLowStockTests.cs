using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceLowStockTests
{
    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndSortsAscendingByStock()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var productA = TestSetup.AddProduct(db, stock: 3, sku: "SKU-A001");
        var productB = TestSetup.AddProduct(db, stock: 8, sku: "SKU-A002");
        TestSetup.AddProduct(db, stock: 15, sku: "SKU-A003");

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal(productA.Sku, result[0].Product.Sku);
        Assert.Equal(productB.Sku, result[1].Product.Sku);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProductsEvenWhenStockIsLower()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var activeProduct = TestSetup.AddProduct(db, stock: 2, sku: "SKU-A001");
        TestSetup.AddProduct(db, stock: 1, isActive: false, sku: "SKU-A002");

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(activeProduct.Sku, result[0].Product.Sku);
    }

    [Fact]
    public async Task GetLowStock_QuantitySoldLast30Days_ExcludesCancelledOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 5, sku: "SKU-A001");

        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 3, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Shipped,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 2, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 100, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(5, result[0].QuantitySoldLast30Days);
    }

    [Fact]
    public async Task GetLowStock_QuantitySoldLast30Days_ExcludesSalesOutsideWindow()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 5, sku: "SKU-A001");

        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-45),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 50, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Items = { new OrderItem { ProductId = product.Id, Quantity = 4, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(4, result[0].QuantitySoldLast30Days);
    }
}

using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    public async Task<IActionResult> LowStock(int threshold = 10)
    {
        var vm = new LowStockViewModel { Threshold = threshold };
        if (!TryValidateModel(vm))
            return View(vm);

        var items = await _productService.GetLowStockAsync(threshold);
        vm.Products = items.Select(x => new LowStockProductRowViewModel
        {
            Sku = x.Product.Sku,
            Name = x.Product.Name,
            StockQuantity = x.Product.StockQuantity,
            QuantitySoldLast30Days = x.QuantitySoldLast30Days
        }).ToList();

        return View(vm);
    }
}


using foxtrot_store_backend.Data;
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Products;
using Microsoft.AspNetCore.Mvc;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController(
        DataAccessor dataAccessor
    ): ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _dataAccessor.GetProductsAsync();

        return Ok(products);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(ProductCreateRequest request)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price,
            Description = request.Description,
            StockQuantity = request.StockQuantity
        };

        var createdProduct = await _dataAccessor.CreateProductAsync(product);

        return Ok(createdProduct);
    }
}
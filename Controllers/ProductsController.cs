
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Products;
using Microsoft.AspNetCore.Mvc;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(DataAccessor dataAccessor) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;

    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] ProductCatalogQuery query)
    {
        if (query.Page < 1)
        {
            ModelState.AddModelError(
                nameof(query.Page),
                "Page must be greater than zero.");
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            ModelState.AddModelError(
                nameof(query.PageSize),
                "PageSize must be between 1 and 100.");
        }

        if (query.MinPrice < 0)
        {
            ModelState.AddModelError(
                nameof(query.MinPrice),
                "MinPrice cannot be negative.");
        }

        if (query.MaxPrice < 0)
        {
            ModelState.AddModelError(
                nameof(query.MaxPrice),
                "MaxPrice cannot be negative.");
        }

        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice > query.MaxPrice)
        {
            ModelState.AddModelError(
                nameof(query.MaxPrice),
                "MaxPrice must be greater than or equal to MinPrice.");
        }

        if (query.Search?.Length > 100)
        {
            ModelState.AddModelError(
                nameof(query.Search),
                "Search cannot exceed 100 characters.");
        }

        var allowedSortFields = new[] { "name", "price", "createdat" };

        if (string.IsNullOrWhiteSpace(query.SortBy) ||
            !allowedSortFields.Contains(
                query.SortBy,
                StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(query.SortBy),
                "SortBy must be name, price or createdAt.");
        }

        if (string.IsNullOrWhiteSpace(query.SortDirection) ||
            !new[] { "asc", "desc" }.Contains(
                query.SortDirection,
                StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(query.SortDirection),
                "SortDirection must be asc or desc.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _dataAccessor.GetProductsAsync(query);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var product = await _dataAccessor.GetProductByIdAsync(id);

        if (product is null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        return Ok(product);
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetProductBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 150)
        {
            return BadRequest(new
            {
                message = "Slug must contain between 1 and 150 characters."
            });
        }

        var product = await _dataAccessor.GetProductBySlugAsync(slug);

        if (product is null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        ProductCreateRequest request)
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

        return CreatedAtAction(
            nameof(GetProductById),
            new { id = createdProduct.Id },
            createdProduct);
    }
}

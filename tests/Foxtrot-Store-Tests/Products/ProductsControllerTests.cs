
using System.Globalization;
using System.Security.Claims;

using foxtrot_store_backend.Controllers;
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Rest;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Foxtrot_Store_Tests.Products;

public sealed class ProductsControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DataContext _dbContext;
    private readonly ProductsController _controller;

    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUICulture;

    public ProductsControllerTests()
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUICulture = CultureInfo.CurrentUICulture;

        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("uk-UA");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("uk-UA");

        // Create an isolated in-memory SQLite database.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new DataContext(options);

        _dbContext.Database.EnsureCreated();

        // The selected GET methods do not use Identity or localization services.
        var dataAccessor = new DataAccessor(
            _dbContext,
            null!,
            null!,
            null!);

        _controller = new ProductsController(
            dataAccessor,
            null!,
            NullLogger<ProductsController>.Instance);

        // Configure an anonymous HTTP user.
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    [Fact]
    public async Task GetProducts_ReturnsOnlyActiveProducts()
    {
        // Arrange
        var activeProduct = await CreateProductAsync(
            "PHONE-001",
            "active-phone",
            "Active Phone",
            isActive: true);

        await CreateProductAsync(
            "PHONE-002",
            "inactive-phone",
            "Inactive Phone",
            isActive: false);

        var query = new ProductCatalogQuery();

        // Act
        var result = await _controller.GetProducts(query);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RestResponse>(okResult.Value);

        Assert.Equal(RestStatus.Ok, response.Status);

        var products = Assert.IsType<List<ProductListItemDto>>(
            response.Data);

        var product = Assert.Single(products);

        Assert.Equal(activeProduct.Id, product.Id);
        Assert.Equal("PHONE-001", product.Sku);
        Assert.Equal("Active Phone", product.Name);
    }

    [Fact]
    public async Task GetProductById_ReturnsExistingProduct()
    {
        // Arrange
        var expectedProduct = await CreateProductAsync(
            "PHONE-001",
            "test-phone",
            "Test Phone");

        // Act
        var result = await _controller.GetProductById(
            expectedProduct.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RestResponse>(okResult.Value);

        Assert.Equal(RestStatus.Ok, response.Status);

        var product = Assert.IsType<ProductDetailsDto>(
            response.Data);

        Assert.Equal(expectedProduct.Id, product.Id);
        Assert.Equal("PHONE-001", product.Sku);
        Assert.Equal("test-phone", product.Slug);
        Assert.Equal("Test Phone", product.Name);
    }

    [Fact]
    public async Task GetProductById_ReturnsNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        var productId = Guid.NewGuid();

        // Act
        var result = await _controller.GetProductById(productId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var response = Assert.IsType<RestResponse>(notFoundResult.Value);

        Assert.Equal(RestStatus.NotFound, response.Status);
    }

    [Fact]
    public async Task GetProductBySlug_ReturnsExistingProduct()
    {
        // Arrange
        var expectedProduct = await CreateProductAsync(
            "PHONE-001",
            "test-phone",
            "Test Phone");

        // Act
        var result = await _controller.GetProductBySlug(
            "  TEST-PHONE  ");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RestResponse>(okResult.Value);

        Assert.Equal(RestStatus.Ok, response.Status);

        var product = Assert.IsType<ProductDetailsDto>(
            response.Data);

        Assert.Equal(expectedProduct.Id, product.Id);
        Assert.Equal("test-phone", product.Slug);
        Assert.Equal("Test Phone", product.Name);
    }

    private async Task<Product> CreateProductAsync(
        string sku,
        string slug,
        string name,
        bool isActive = true)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Slug = slug,
            Price = 1000m,
            StockQuantity = 10,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        product.Translations.Add(new ProductTranslation
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            CultureCode = "uk-UA",
            Name = name,
            Description = "Test product description",
            Product = product
        });

        _dbContext.Products.Add(product);

        await _dbContext.SaveChangesAsync();

        return product;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();

        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUICulture;
    }
}

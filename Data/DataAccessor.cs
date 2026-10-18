
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Products;
using Microsoft.EntityFrameworkCore;

namespace foxtrot_store_backend.Data;

public class DataAccessor(DataContext dataContext)
{
    private readonly DataContext _dataContext = dataContext;

    public Guid GetDbIdentity()
    {
        return _dataContext.Database
            .SqlQuery<Guid>($"SELECT NEWID()")
            .AsEnumerable()
            .First();
    }

    public async Task<ProductCatalogResponseDto> GetProductsAsync(
        ProductCatalogQuery query)
    {
        IQueryable<Product> products = _dataContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            products = products.Where(product =>
                product.Name.Contains(search) ||
                product.Sku.Contains(search) ||
                product.Description.Contains(search));
        }

        if (query.MinPrice.HasValue)
        {
            products = products.Where(product =>
                product.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            products = products.Where(product =>
                product.Price <= query.MaxPrice.Value);
        }

        if (query.BrandId.HasValue)
        {
            products = products.Where(product =>
                product.BrandId == query.BrandId.Value &&
                product.Brand != null &&
                product.Brand.IsActive);
        }

        if (query.CategoryId.HasValue)
        {
            products = products.Where(product =>
                product.ProductCategories.Any(productCategory =>
                    productCategory.CategoryId == query.CategoryId.Value &&
                    productCategory.Category.IsActive));
        }

        if (query.InStock == true)
        {
            products = products.Where(product =>
                product.StockQuantity > 0);
        }
        else if (query.InStock == false)
        {
            products = products.Where(product =>
                product.StockQuantity <= 0);
        }

        products = (
            query.SortBy.ToLowerInvariant(),
            query.SortDirection.ToLowerInvariant()
        ) switch
        {
            ("price", "asc") => products
                .OrderBy(product => product.Price)
                .ThenBy(product => product.Id),

            ("price", "desc") => products
                .OrderByDescending(product => product.Price)
                .ThenBy(product => product.Id),

            ("createdat", "asc") => products
                .OrderBy(product => product.CreatedAt)
                .ThenBy(product => product.Id),

            ("createdat", "desc") => products
                .OrderByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Id),

            ("name", "desc") => products
                .OrderByDescending(product => product.Name)
                .ThenBy(product => product.Id),

            _ => products
                .OrderBy(product => product.Name)
                .ThenBy(product => product.Id)
        };

        var totalCount = await products.CountAsync();

        var items = await products
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(product => new ProductListItemDto
            {
                Id = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                Slug = product.Slug,
                Price = product.Price,
                StockQuantity = product.StockQuantity,

                BrandName = product.Brand != null &&
                            product.Brand.IsActive
                    ? product.Brand.Name
                    : null,

                PrimaryImageUrl = product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault(),

                Categories = product.ProductCategories
                    .Where(productCategory =>
                        productCategory.Category.IsActive)
                    .OrderBy(productCategory =>
                        productCategory.Category.SortOrder)
                    .Select(productCategory =>
                        productCategory.Category.Name)
                    .ToList()
            })
            .ToListAsync();

        return new ProductCatalogResponseDto
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(
                totalCount / (double)query.PageSize)
        };
    }

    public async Task<ProductDetailsDto?> GetProductByIdAsync(Guid id)
    {
        return await _dataContext.Products
            .AsNoTracking()
            .Where(product =>
                product.Id == id &&
                product.IsActive)
            .Select(product => new ProductDetailsDto
            {
                Id = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,

                BrandName = product.Brand != null &&
                            product.Brand.IsActive
                    ? product.Brand.Name
                    : null,

                Categories = product.ProductCategories
                    .Where(productCategory =>
                        productCategory.Category.IsActive)
                    .OrderBy(productCategory =>
                        productCategory.Category.SortOrder)
                    .Select(productCategory =>
                        productCategory.Category.Name)
                    .ToList(),

                Images = product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => new ProductImageDto
                    {
                        ImageUrl = image.ImageUrl,
                        IsPrimary = image.IsPrimary,
                        SortOrder = image.SortOrder
                    })
                    .ToList(),

                Specifications = product.Specifications
                    .OrderBy(specification => specification.SortOrder)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Name = specification.Name,
                        Value = specification.Value,
                        SortOrder = specification.SortOrder
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ProductDetailsDto?> GetProductBySlugAsync(
        string slug)
    {
        return await _dataContext.Products
            .AsNoTracking()
            .Where(product =>
                product.Slug == slug &&
                product.IsActive)
            .Select(product => new ProductDetailsDto
            {
                Id = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,

                BrandName = product.Brand != null &&
                            product.Brand.IsActive
                    ? product.Brand.Name
                    : null,

                Categories = product.ProductCategories
                    .Where(productCategory =>
                        productCategory.Category.IsActive)
                    .OrderBy(productCategory =>
                        productCategory.Category.SortOrder)
                    .Select(productCategory =>
                        productCategory.Category.Name)
                    .ToList(),

                Images = product.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => new ProductImageDto
                    {
                        ImageUrl = image.ImageUrl,
                        IsPrimary = image.IsPrimary,
                        SortOrder = image.SortOrder
                    })
                    .ToList(),

                Specifications = product.Specifications
                    .OrderBy(specification => specification.SortOrder)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Name = specification.Name,
                        Value = specification.Value,
                        SortOrder = specification.SortOrder
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }


    public async Task<ProductDetailsDto> CreateProductAsync(
        ProductCreateRequest request)
    {
        var sku = request.Sku.Trim();
        var slug = request.Slug.Trim().ToLowerInvariant();

        // Check for duplicate SKU and slug.
        if (await _dataContext.Products.AnyAsync(
                product => product.Sku == sku))
        {
            throw new InvalidOperationException(
                "A product with this SKU already exists.");
        }

        if (await _dataContext.Products.AnyAsync(
                product => product.Slug == slug))
        {
            throw new InvalidOperationException(
                "A product with this slug already exists.");
        }

        // Validate the brand.
        if (request.BrandId.HasValue)
        {
            var brandExists = await _dataContext.Brands.AnyAsync(
                brand => brand.Id == request.BrandId.Value
                         && brand.IsActive);

            if (!brandExists)
            {
                throw new ArgumentException(
                    "The specified brand does not exist or is inactive.");
            }
        }

        // Validate categories.
        var categoryIds = request.CategoryIds.Distinct().ToList();

        if (categoryIds.Count != request.CategoryIds.Count)
        {
            throw new ArgumentException(
                "Duplicate category IDs are not allowed.");
        }

        if (categoryIds.Count > 0)
        {
            var activeCategoryCount =
                await _dataContext.Categories.CountAsync(
                    category => categoryIds.Contains(category.Id)
                                && category.IsActive);

            if (activeCategoryCount != categoryIds.Count)
            {
                throw new ArgumentException(
                    "One or more categories do not exist or are inactive.");
            }
        }

        // Allow at most one primary image.
        if (request.Images.Count(image => image.IsPrimary) > 1)
        {
            throw new ArgumentException(
                "Only one primary image is allowed.");
        }

        var now = DateTime.UtcNow;

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = sku,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            BrandId = request.BrandId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dataContext.Products.Add(product);

        foreach (var categoryId in categoryIds)
        {
            _dataContext.ProductCategories.Add(new ProductCategory
            {
                ProductId = product.Id,
                CategoryId = categoryId
            });
        }

        foreach (var image in request.Images)
        {
            _dataContext.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ImageUrl = image.ImageUrl.Trim(),
                IsPrimary = image.IsPrimary,
                SortOrder = image.SortOrder
            });
        }

        foreach (var specification in request.Specifications)
        {
            _dataContext.ProductSpecifications.Add(
                new ProductSpecification
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Name = specification.Name.Trim(),
                    Value = specification.Value.Trim(),
                    SortOrder = specification.SortOrder
                });
        }

        // EF Core uses a transaction for this relational SaveChanges operation.
        await _dataContext.SaveChangesAsync();

        var createdProduct = await GetProductByIdAsync(product.Id);

        return createdProduct
            ?? throw new InvalidOperationException(
                "The product was created but could not be retrieved.");
    }

}

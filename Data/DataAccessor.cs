
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Brands;
using foxtrot_store_backend.Models.Categories;
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


    public async Task<List<BrandDto>> GetBrandsAsync()
    {
        return await _dataContext.Brands
            .AsNoTracking()
            .Where(brand => brand.IsActive)
            .OrderBy(brand => brand.Name)
            .Select(brand => new BrandDto
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                Description = brand.Description,
                LogoUrl = brand.LogoUrl
            })
            .ToListAsync();
    }

    public async Task<BrandDto?> GetBrandByIdAsync(Guid id)
    {
        return await _dataContext.Brands
            .AsNoTracking()
            .Where(brand => brand.Id == id && brand.IsActive)
            .Select(brand => new BrandDto
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                Description = brand.Description,
                LogoUrl = brand.LogoUrl
            })
            .FirstOrDefaultAsync();
    }

    public async Task<BrandDto?> GetBrandBySlugAsync(string slug)
    {
        return await _dataContext.Brands
            .AsNoTracking()
            .Where(brand => brand.Slug == slug && brand.IsActive)
            .Select(brand => new BrandDto
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                Description = brand.Description,
                LogoUrl = brand.LogoUrl
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> BrandExistsAsync(Guid id)
    {
        return await _dataContext.Brands
            .AsNoTracking()
            .AnyAsync(brand => brand.Id == id && brand.IsActive);
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        return await _dataContext.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ParentCategoryId = category.ParentCategoryId,
                SortOrder = category.SortOrder
            })
            .ToListAsync();
    }

    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        return await _dataContext.Categories
            .AsNoTracking()
            .Where(category => category.Id == id && category.IsActive)
            .Select(category => new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ParentCategoryId = category.ParentCategoryId,
                SortOrder = category.SortOrder
            })
            .FirstOrDefaultAsync();
    }

    public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
    {
        return await _dataContext.Categories
            .AsNoTracking()
            .Where(category => category.Slug == slug && category.IsActive)
            .Select(category => new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                Description = category.Description,
                ParentCategoryId = category.ParentCategoryId,
                SortOrder = category.SortOrder
            })
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CategoryExistsAsync(Guid id)
    {
        return await _dataContext.Categories
            .AsNoTracking()
            .AnyAsync(category => category.Id == id && category.IsActive);
    }

    public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync()
    {
        var categories = await GetCategoriesAsync();

        var nodes = categories.ToDictionary(
            category => category.Id,
            category => new CategoryTreeDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                SortOrder = category.SortOrder
            });

        var roots = new List<CategoryTreeDto>();

        foreach (var category in categories)
        {
            var node = nodes[category.Id];

            if (category.ParentCategoryId.HasValue &&
                nodes.TryGetValue(category.ParentCategoryId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }

        SortCategoryTree(roots);

        return roots;
    }

    private static void SortCategoryTree(List<CategoryTreeDto> categories)
    {
        categories.Sort((first, second) =>
        {
            var orderComparison = first.SortOrder.CompareTo(second.SortOrder);

            return orderComparison != 0
                ? orderComparison
                : string.Compare(
                    first.Name,
                    second.Name,
                    StringComparison.OrdinalIgnoreCase);
        });

        foreach (var category in categories)
        {
            SortCategoryTree(category.Children);
        }
    }


    // Brands

    public async Task<BrandDto> CreateBrandAsync(
        BrandCreateRequest request)
    {
        var name = request.Name.Trim();
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await _dataContext.Brands.AnyAsync(b => b.Slug == slug))
        {
            throw new InvalidOperationException(
                "A brand with this slug already exists.");
        }

        var brand = new Brand
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            Description = request.Description?.Trim(),
            LogoUrl = request.LogoUrl?.Trim(),
            IsActive = true
        };

        _dataContext.Brands.Add(brand);
        await _dataContext.SaveChangesAsync();

        return new BrandDto
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            Description = brand.Description,
            LogoUrl = brand.LogoUrl
        };
    }

    public async Task<BrandDto?> UpdateBrandAsync(
        Guid id,
        BrandUpdateRequest request)
    {
        var brand = await _dataContext.Brands
            .FirstOrDefaultAsync(b => b.Id == id);

        if (brand is null)
        {
            return null;
        }

        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await _dataContext.Brands.AnyAsync(
            b => b.Slug == slug && b.Id != id))
        {
            throw new InvalidOperationException(
                "A brand with this slug already exists.");
        }

        brand.Name = request.Name.Trim();
        brand.Slug = slug;
        brand.Description = request.Description?.Trim();
        brand.LogoUrl = request.LogoUrl?.Trim();
        brand.IsActive = request.IsActive;

        await _dataContext.SaveChangesAsync();

        return new BrandDto
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            Description = brand.Description,
            LogoUrl = brand.LogoUrl
        };
    }

    public async Task<bool> DeleteBrandAsync(Guid id)
    {
        var brand = await _dataContext.Brands
            .FirstOrDefaultAsync(b => b.Id == id);

        if (brand is null)
        {
            return false;
        }

        _dataContext.Brands.Remove(brand);
        await _dataContext.SaveChangesAsync();

        return true;
    }

    // Categories

    public async Task<CategoryDto> CreateCategoryAsync(
        CategoryCreateRequest request)
    {
        var id = Guid.NewGuid();
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await _dataContext.Categories.AnyAsync(
            c => c.Slug == slug))
        {
            throw new InvalidOperationException(
                "A category with this slug already exists.");
        }

        await EnsureValidParentCategoryAsync(
            id,
            request.ParentCategoryId);

        var category = new Category
        {
            Id = id,
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            ParentCategoryId = request.ParentCategoryId,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _dataContext.Categories.Add(category);
        await _dataContext.SaveChangesAsync();

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ParentCategoryId = category.ParentCategoryId,
            SortOrder = category.SortOrder
        };
    }

    public async Task<CategoryDto?> UpdateCategoryAsync(
        Guid id,
        CategoryUpdateRequest request)
    {
        var category = await _dataContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
        {
            return null;
        }

        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await _dataContext.Categories.AnyAsync(
            c => c.Slug == slug && c.Id != id))
        {
            throw new InvalidOperationException(
                "A category with this slug already exists.");
        }

        await EnsureValidParentCategoryAsync(
            id,
            request.ParentCategoryId);

        category.Name = request.Name.Trim();
        category.Slug = slug;
        category.Description = request.Description?.Trim();
        category.ParentCategoryId = request.ParentCategoryId;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        await _dataContext.SaveChangesAsync();

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ParentCategoryId = category.ParentCategoryId,
            SortOrder = category.SortOrder
        };
    }

    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var category = await _dataContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category is null)
        {
            return false;
        }

        _dataContext.Categories.Remove(category);
        await _dataContext.SaveChangesAsync();

        return true;
    }

    private async Task EnsureValidParentCategoryAsync(
        Guid categoryId,
        Guid? parentCategoryId)
    {
        if (!parentCategoryId.HasValue)
        {
            return;
        }

        var parents = await _dataContext.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new
            {
                c.Id,
                c.ParentCategoryId
            })
            .ToDictionaryAsync(c => c.Id, c => c.ParentCategoryId);

        if (!parents.ContainsKey(parentCategoryId.Value))
        {
            throw new ArgumentException(
                "The parent category does not exist or is inactive.");
        }

        var visited = new HashSet<Guid>();
        Guid? currentId = parentCategoryId;

        while (currentId.HasValue)
        {
            if (currentId.Value == categoryId)
            {
                throw new ArgumentException(
                    "A category cannot be its own ancestor.");
            }

            if (!visited.Add(currentId.Value))
            {
                throw new ArgumentException(
                    "The category hierarchy contains a cycle.");
            }

            if (!parents.TryGetValue(currentId.Value, out currentId))
            {
                break;
            }
        }
    }

}

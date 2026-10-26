using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Brands;
using foxtrot_store_backend.Models.Categories;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using foxtrot_store_backend.Models.Auth;
using foxtrot_store_backend.Constants;

namespace foxtrot_store_backend.Data;

public class DataAccessor(
    DataContext dataContext,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    private readonly DataContext _dataContext = dataContext;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

    public Task<Guid> GetDbIdentityAsync()
    {
        return _dataContext.Database
            .SqlQuery<Guid>($"SELECT UUID() AS Value")
            .SingleAsync();
    }

    public async Task<bool> AddProductImageAsync(
    Guid productId,
    string imageKey,
    bool isPrimary,
    int sortOrder,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageKey))
        {
            throw new ArgumentException(
                "Image key cannot be empty.",
                nameof(imageKey));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "SortOrder cannot be negative.");
        }

        var product = await _dataContext.Products
            .Include(product => product.Images)
            .FirstOrDefaultAsync(
                product => product.Id == productId && product.IsActive,
                cancellationToken);

        if (product is null)
        {
            return false;
        }

        // A product can have at most one primary image.
        if (isPrimary)
        {
            foreach (var existingImage in product.Images)
            {
                existingImage.IsPrimary = false;
            }
        }

        var image = new ProductImage
        {
            Id = await GetDbIdentityAsync(),
            ProductId = product.Id,
            ImageUrl = imageKey.Trim(),
            IsPrimary = isPrimary,
            SortOrder = sortOrder
        };

        _dataContext.ProductImages.Add(image);

        product.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        return true;
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
                IsActive = product.IsActive,
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
                IsActive = product.IsActive,
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

        // Validate specification names.
        var specificationNames = request.Specifications
            .Select(specification => specification.Name.Trim())
            .ToList();

        if (specificationNames.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != specificationNames.Count)
        {
            throw new ArgumentException(
                "Duplicate specification names are not allowed.");
        }

        var now = DateTime.UtcNow;

        var productId = await GetDbIdentityAsync();
        var product = new Product
        {
            Id = productId,
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

        foreach (var specification in request.Specifications)
        {
            _dataContext.ProductSpecifications.Add(
                new ProductSpecification
                {
                    Id = await GetDbIdentityAsync(),
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
    public async Task<ProductDetailsDto?> GetProductForAdminByIdAsync(Guid id)
    {
        return await _dataContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(product => new ProductDetailsDto
            {
                Id = product.Id,
                Sku = product.Sku,
                Name = product.Name,
                Slug = product.Slug,
                Description = product.Description,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive,

                BrandName = product.Brand != null && product.Brand.IsActive
                    ? product.Brand.Name
                    : null,

                Categories = product.ProductCategories
                    .Where(pc => pc.Category.IsActive)
                    .OrderBy(pc => pc.Category.SortOrder)
                    .Select(pc => pc.Category.Name)
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
    public async Task<ProductDetailsDto?> UpdateProductAsync(
        Guid id,
        ProductUpdateRequest request)
    {
        var product = await _dataContext.Products
             .Include(p => p.ProductCategories)
             .Include(p => p.Specifications)
             .FirstOrDefaultAsync(p => p.Id == id);

        if (product is null)
        {
            return null;
        }

        var sku = request.Sku.Trim();
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await _dataContext.Products.AnyAsync(
            p => p.Id != id && p.Sku == sku))
        {
            throw new InvalidOperationException(
                "A product with this SKU already exists.");
        }

        if (await _dataContext.Products.AnyAsync(
            p => p.Id != id && p.Slug == slug))
        {
            throw new InvalidOperationException(
                "A product with this slug already exists.");
        }

        if (request.BrandId.HasValue)
        {
            var brandExists = await _dataContext.Brands.AnyAsync(
                brand => brand.Id == request.BrandId.Value && brand.IsActive);

            if (!brandExists)
            {
                throw new ArgumentException(
                    "The specified brand does not exist or is inactive.");
            }
        }

        var categoryIds = request.CategoryIds.Distinct().ToList();

        if (categoryIds.Count != request.CategoryIds.Count)
        {
            throw new ArgumentException(
                "Duplicate category IDs are not allowed.");
        }

        if (categoryIds.Count > 0)
        {
            var activeCategoryCount = await _dataContext.Categories.CountAsync(
                category => categoryIds.Contains(category.Id) && category.IsActive);

            if (activeCategoryCount != categoryIds.Count)
            {
                throw new ArgumentException(
                    "One or more categories do not exist or are inactive.");
            }
        }

        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Slug = slug;
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.BrandId = request.BrandId;
        product.IsActive = request.IsActive ?? product.IsActive;
        product.UpdatedAt = DateTime.UtcNow;

        // Update category links by difference. ProductCategory commonly uses a
        // composite key, so do not delete and re-add the same keys in one context.
        var requestedCategoryIdSet = categoryIds.ToHashSet();
        var linksToRemove = product.ProductCategories
            .Where(link => !requestedCategoryIdSet.Contains(link.CategoryId))
            .ToList();
        _dataContext.ProductCategories.RemoveRange(linksToRemove);

        var currentCategoryIds = product.ProductCategories
            .Select(link => link.CategoryId)
            .ToHashSet();

        foreach (var categoryId in categoryIds.Where(
            categoryId => !currentCategoryIds.Contains(categoryId)))
        {
            _dataContext.ProductCategories.Add(new ProductCategory
            {
                ProductId = product.Id,
                CategoryId = categoryId
            });
        }

        _dataContext.ProductSpecifications.RemoveRange(
            product.Specifications);

        foreach (var specification in request.Specifications)
        {
            _dataContext.ProductSpecifications.Add(new ProductSpecification
            {
                Id = await GetDbIdentityAsync(),
                ProductId = product.Id,
                Name = specification.Name.Trim(),
                Value = specification.Value.Trim(),
                SortOrder = specification.SortOrder
            });
        }

        await _dataContext.SaveChangesAsync();

        return await GetProductForAdminByIdAsync(id)
            ?? throw new InvalidOperationException(
                "The product was updated but could not be retrieved.");
    }
    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var product = await _dataContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

        if (product is null)
        {
            return false;
        }

        // Soft delete: public catalog queries already filter IsActive == true.
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync();
        return true;
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
                ImageUrl = category.ImageUrl,
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
                ImageUrl = category.ImageUrl,
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
                ImageUrl = category.ImageUrl,
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
                SortOrder = category.SortOrder,
                ImageUrl = category.ImageUrl,
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
            ImageUrl = request.ImageUrl?.Trim(),
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
            ImageUrl = category.ImageUrl,
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
        category.ImageUrl = request.ImageUrl?.Trim();
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
            ImageUrl = category.ImageUrl,
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


    // Promotions
    public async Task<List<PromotionDto>> GetPromotionsAsync()
    {
        var now = DateTime.UtcNow;

        return await _dataContext.Promotions
            .AsNoTracking()
            .Where(promotion =>
                promotion.IsActive &&
                (!promotion.StartDate.HasValue ||
                 promotion.StartDate.Value <= now) &&
                (!promotion.EndDate.HasValue ||
                 promotion.EndDate.Value >= now))
            .OrderBy(promotion => promotion.SortOrder)
            .ThenBy(promotion => promotion.Title)
            .Select(promotion => new PromotionDto
            {
                Id = promotion.Id,
                Title = promotion.Title,
                Slug = promotion.Slug,
                BannerImageUrl = promotion.BannerImageUrl,
                SortOrder = promotion.SortOrder
            })
            .ToListAsync();
    }
    public async Task<PromotionDetailsDto?> GetPromotionByIdAsync(
        Guid id)
    {
        var now = DateTime.UtcNow;

        return await _dataContext.Promotions
            .AsNoTracking()
            .Where(promotion =>
                promotion.Id == id &&
                promotion.IsActive &&
                (!promotion.StartDate.HasValue ||
                 promotion.StartDate.Value <= now) &&
                (!promotion.EndDate.HasValue ||
                 promotion.EndDate.Value >= now))
            .Select(promotion => new PromotionDetailsDto
            {
                Id = promotion.Id,
                Title = promotion.Title,
                Slug = promotion.Slug,
                Description = promotion.Description,
                BannerImageUrl = promotion.BannerImageUrl,
                ImageUrl = promotion.ImageUrl,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate
            })
            .FirstOrDefaultAsync();
    }
    public async Task<PromotionDetailsDto?> GetPromotionBySlugAsync(
        string slug)
    {
        var now = DateTime.UtcNow;
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        return await _dataContext.Promotions
            .AsNoTracking()
            .Where(promotion =>
                promotion.Slug == normalizedSlug &&
                promotion.IsActive &&
                (!promotion.StartDate.HasValue ||
                 promotion.StartDate.Value <= now) &&
                (!promotion.EndDate.HasValue ||
                 promotion.EndDate.Value >= now))
            .Select(promotion => new PromotionDetailsDto
            {
                Id = promotion.Id,
                Title = promotion.Title,
                Slug = promotion.Slug,
                Description = promotion.Description,
                BannerImageUrl = promotion.BannerImageUrl,
                ImageUrl = promotion.ImageUrl,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate
            })
            .FirstOrDefaultAsync();
    }
    public async Task<List<PromotionAdminDto>> GetPromotionsForAdminAsync()
    {
        return await _dataContext.Promotions
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Title)
            .Select(p => new PromotionAdminDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Description = p.Description,
                BannerImageUrl = p.BannerImageUrl,
                ImageUrl = p.ImageUrl,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .ToListAsync();
    }
    public async Task<PromotionAdminDto?> GetPromotionForAdminByIdAsync(
        Guid id)
    {
        return await _dataContext.Promotions
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PromotionAdminDto
            {
                Id = p.Id,
                Title = p.Title,
                Slug = p.Slug,
                Description = p.Description,
                BannerImageUrl = p.BannerImageUrl,
                ImageUrl = p.ImageUrl,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            })
            .FirstOrDefaultAsync();
    }
    public async Task<bool> PromotionSlugExistsAsync(
        string slug,
        Guid? excludeId = null)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var query = _dataContext.Promotions
            .AsNoTracking()
            .Where(p => p.Slug == normalizedSlug);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }
    public async Task<PromotionAdminDto> CreatePromotionAsync(
        PromotionCreateRequest request)
    {
        ValidatePromotionDates(request.StartDate, request.EndDate);

        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await PromotionSlugExistsAsync(slug))
        {
            throw new InvalidOperationException(
                "A promotion with this slug already exists.");
        }

        var promotion = new foxtrot_store_backend.Data.Entities.Promotion
        {
            Title = request.Title.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            BannerImageUrl = request.BannerImageUrl?.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAt = DateTime.UtcNow
        };

        _dataContext.Promotions.Add(promotion);
        await _dataContext.SaveChangesAsync();

        return (await GetPromotionForAdminByIdAsync(promotion.Id))!;
    }
    public async Task<PromotionAdminDto?> UpdatePromotionAsync(
        Guid id,
        PromotionUpdateRequest request)
    {
        ValidatePromotionDates(request.StartDate, request.EndDate);

        var promotion = await _dataContext.Promotions
            .FirstOrDefaultAsync(p => p.Id == id);

        if (promotion is null)
        {
            return null;
        }

        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await PromotionSlugExistsAsync(slug, id))
        {
            throw new InvalidOperationException(
                "A promotion with this slug already exists.");
        }

        promotion.Title = request.Title.Trim();
        promotion.Slug = slug;
        promotion.Description = request.Description?.Trim();
        promotion.BannerImageUrl = request.BannerImageUrl?.Trim();
        promotion.ImageUrl = request.ImageUrl?.Trim();
        promotion.StartDate = request.StartDate;
        promotion.EndDate = request.EndDate;
        promotion.IsActive = request.IsActive;
        promotion.SortOrder = request.SortOrder;
        promotion.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync();

        return await GetPromotionForAdminByIdAsync(id);
    }
    public async Task<bool> DeletePromotionAsync(Guid id)
    {
        var promotion = await _dataContext.Promotions
            .FirstOrDefaultAsync(p => p.Id == id);

        if (promotion is null)
        {
            return false;
        }

        promotion.IsActive = false;
        promotion.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync();

        return true;
    }
    private static void ValidatePromotionDates(
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue &&
            endDate.HasValue &&
            endDate.Value < startDate.Value)
        {
            throw new ArgumentException(
                "EndDate cannot be earlier than StartDate.");
        }
    }


    #region User Management

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync()
    {
        var users = _userManager.Users
            .OrderBy(user => user.UserName)
            .ToList();

        var result = new List<UserDto>();

        foreach (var user in users)
        {
            result.Add(await MapUserAsync(user));
        }

        return result;
    }
    public async Task<UserDto?> GetUserByIdAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        return user is null ? null : await MapUserAsync(user);
    }
    public async Task<IdentityUser?> FindUserByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }
    public async Task<IdentityUser?> FindUserByIdAsync(string id)
    {
        return await _userManager.FindByIdAsync(id);
    }
    public async Task<(IdentityUser? User, IdentityResult Result)> CreateUserAsync(
    string userName,
    string email,
    string password,
    string? role = null)
    {
        var selectedRole = string.IsNullOrWhiteSpace(role)
            ? AppRoles.User
            : role.Trim();

        if (!AppRoles.All.Contains(selectedRole, StringComparer.OrdinalIgnoreCase))
        {
            return (null, IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidRole",
                Description = "Допустимые роли: User или Admin."
            }));
        }

        selectedRole = AppRoles.All.First(r =>
            string.Equals(r, selectedRole, StringComparison.OrdinalIgnoreCase));

        if (!await _roleManager.RoleExistsAsync(selectedRole))
        {
            return (null, IdentityResult.Failed(new IdentityError
            {
                Code = "RoleNotFound",
                Description = $"Роль '{selectedRole}' не существует в базе данных."
            }));
        }

        var user = new IdentityUser
        {
            UserName = userName.Trim(),
            Email = email.Trim()
        };

        var createResult = await _userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
            return (null, createResult);

        var roleResult = await _userManager.AddToRoleAsync(user, selectedRole);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return (null, roleResult);
        }

        return (user, IdentityResult.Success);
    }
    public async Task<IdentityResult> UpdateUserAsync(
        string id,
        string userName,
        string email)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNotFound",
                Description = "User was not found."
            });
        }

        user.UserName = userName.Trim();
        user.Email = email.Trim();

        return await _userManager.UpdateAsync(user);
    }
    public async Task<IdentityResult> UpdateUserRoleAsync(
        string userId,
        string role)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNotFound",
                Description = "Пользователь не найден."
            });
        }

        if (string.IsNullOrWhiteSpace(role))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidRole",
                Description = "Укажите роль User или Admin."
            });
        }

        var selectedRole = AppRoles.All.FirstOrDefault(r =>
            string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));

        if (selectedRole is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidRole",
                Description = "Допустимые роли: User или Admin."
            });
        }

        if (!await _roleManager.RoleExistsAsync(selectedRole))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "RoleNotFound",
                Description = $"Роль '{selectedRole}' не существует."
            });
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Count == 1 && currentRoles[0] == selectedRole)
            return IdentityResult.Success;

        if (currentRoles.Contains(AppRoles.Admin) &&
            selectedRole != AppRoles.Admin)
        {
            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);

            if (admins.Count <= 1)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "LastAdmin",
                    Description = "Нельзя снять роль Admin с последнего администратора."
                });
            }
        }

        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(
                user,
                currentRoles);

            if (!removeResult.Succeeded)
                return removeResult;
        }

        var addResult = await _userManager.AddToRoleAsync(user, selectedRole);

        if (!addResult.Succeeded)
        {
            if (currentRoles.Count > 0)
                await _userManager.AddToRolesAsync(user, currentRoles);

            return addResult;
        }

        return IdentityResult.Success;
    }
    public async Task<IdentityResult> UpdateUserProfileAsync(
        string id,
        string userName,
        string email)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNotFound",
                Description = "User was not found."
            });
        }

        user.UserName = userName.Trim();
        user.Email = email.Trim();

        return await _userManager.UpdateAsync(user);
    }
    public async Task<IdentityResult> ChangeUserPasswordAsync(
        string id,
        string currentPassword,
        string newPassword)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNotFound",
                Description = "User was not found."
            });
        }

        return await _userManager.ChangePasswordAsync(
            user,
            currentPassword,
            newPassword);
    }
    public async Task<IdentityResult> DeleteUserAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);

        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserNotFound",
                Description = "User was not found."
            });
        }

        return await _userManager.DeleteAsync(user);
    }
    private async Task<UserDto> MapUserAsync(IdentityUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            IsLockedOut = await _userManager.IsLockedOutAsync(user),
            Roles = roles.ToList()
        };
    }

    public async Task EnsureRolesCreatedAsync()
    {
        foreach (var role in AppRoles.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var result = await _roleManager.CreateAsync(
                    new IdentityRole(role));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        "; ",
                        result.Errors.Select(e => e.Description));

                    throw new InvalidOperationException(
                        $"Не удалось создать роль '{role}': {errors}");
                }
            }
        }
    }
    #endregion
}

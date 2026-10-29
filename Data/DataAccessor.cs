using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Brands;
using foxtrot_store_backend.Models.Categories;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using foxtrot_store_backend.Models.Auth;
using foxtrot_store_backend.Constants;
using MySql.Data.MySqlClient;

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


    // Products
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
        if (query.Page < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.Page),
                "Page must be greater than zero.");
        }

        if (query.PageSize < 1 || query.PageSize > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.PageSize),
                "PageSize must be between 1 and 100.");
        }

        var offset = ((long)query.Page - 1) * query.PageSize;

        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(query.Page),
                "The requested page is too large.");
        }

        var skip = (int)offset;

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
            .Skip(skip)
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

        var promotionDiscounts = await GetActivePromotionDiscountsAsync(
            items.Select(item => item.Id));

        foreach (var item in items)
        {
            item.ActivePromotion = CalculateBestPromotionPrice(
                item.Price,
                promotionDiscounts.GetValueOrDefault(item.Id));
        }

        foreach (var item in items)
        {
            if (item.PrimaryImageUrl is not null)
            {
                item.PrimaryImageUrl =
                    BuildProductImageUrl(item.PrimaryImageUrl);
            }
        }

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
        var product = await _dataContext.Products
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
                    .ThenBy(specification => specification.Name)
                    .ThenBy(specification => specification.Id)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Id = specification.Id,
                        Name = specification.Name,
                        Value = specification.Value,
                        SortOrder = specification.SortOrder
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (product is not null)
        {
            var promotionDiscounts = await GetActivePromotionDiscountsAsync(
                new[] { product.Id });

            product.ActivePromotion = CalculateBestPromotionPrice(
                product.Price,
                promotionDiscounts.GetValueOrDefault(product.Id));

            foreach (var image in product.Images)
            {
                image.ImageUrl = BuildProductImageUrl(image.ImageUrl);
            }
        }

        return product;
    }
    public async Task<ProductDetailsDto?> GetProductBySlugAsync(
        string slug)
    {
        var product = await _dataContext.Products
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
                    .ThenBy(specification => specification.Name)
                    .ThenBy(specification => specification.Id)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Id = specification.Id,
                        Name = specification.Name,
                        Value = specification.Value,
                        SortOrder = specification.SortOrder
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (product is not null)
        {
            var promotionDiscounts = await GetActivePromotionDiscountsAsync(
                new[] { product.Id });

            product.ActivePromotion = CalculateBestPromotionPrice(
                product.Price,
                promotionDiscounts.GetValueOrDefault(product.Id));

            foreach (var image in product.Images)
            {
                image.ImageUrl = BuildProductImageUrl(image.ImageUrl);
            }
        }

        return product;
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
        if (request.Specifications is null)
        {
            throw new ArgumentException(
                "Specifications cannot be null.");
        }

        var specifications = request.Specifications
            .Select(specification =>
            {
                var (name, value) = ValidateAndNormalizeSpecification(
                    specification.Name,
                    specification.Value,
                    specification.SortOrder);

                return new
                {
                    Name = name,
                    Value = value,
                    specification.SortOrder
                };
            })
            .ToList();

        var specificationNames = specifications
            .Select(specification => specification.Name)
            .ToList();

        if (specificationNames.Distinct(
                StringComparer.OrdinalIgnoreCase).Count()
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

        foreach (var specification in specifications)
        {
            _dataContext.ProductSpecifications.Add(
                new ProductSpecification
                {
                    Id = await GetDbIdentityAsync(),
                    ProductId = product.Id,
                    Name = specification.Name,
                    Value = specification.Value,
                    SortOrder = specification.SortOrder
                });
        }

        await SaveChangesOrThrowConflictAsync();

        var createdProduct = await GetProductByIdAsync(product.Id);

        return createdProduct
            ?? throw new InvalidOperationException(
                "The product was created but could not be retrieved.");
    }
    public async Task<ProductDetailsDto?> GetProductForAdminByIdAsync(
        Guid id)
    {
        var product = await _dataContext.Products
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
                    .ThenBy(specification => specification.Name)
                    .ThenBy(specification => specification.Id)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Id = specification.Id,
                        Name = specification.Name,
                        Value = specification.Value,
                        SortOrder = specification.SortOrder
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (product is not null)
        {
            var promotionDiscounts = await GetActivePromotionDiscountsAsync(
                new[] { product.Id });

            product.ActivePromotion = CalculateBestPromotionPrice(
                product.Price,
                promotionDiscounts.GetValueOrDefault(product.Id));

            foreach (var image in product.Images)
            {
                image.ImageUrl = BuildProductImageUrl(image.ImageUrl);
            }
        }

        return product;
    }
    public async Task<ProductDetailsDto?> UpdateProductAsync(
    Guid id,
    ProductUpdateRequest request)
    {
        // Includes inactive products intentionally:
        // administrators may edit archived products.
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
                brand => brand.Id == request.BrandId.Value
                         && brand.IsActive);

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

        // Normalize and validate specification names before modifying the product.
        if (request.Specifications is null)
        {
            throw new ArgumentException(
                "Specifications cannot be null.");
        }

        var specifications = request.Specifications
            .Select(specification =>
            {
                var (name, value) = ValidateAndNormalizeSpecification(
                    specification.Name,
                    specification.Value,
                    specification.SortOrder);

                return new
                {
                    Name = name,
                    Value = value,
                    specification.SortOrder
                };
            })
            .ToList();

        var specificationNames = specifications
            .Select(specification => specification.Name)
            .ToList();

        if (specificationNames.Distinct(
                StringComparer.OrdinalIgnoreCase).Count()
            != specificationNames.Count)
        {
            throw new ArgumentException(
                "Duplicate specification names are not allowed.");
        }

        // Update product fields. IsActive is deliberately not changed here.
        product.Sku = sku;
        product.Name = request.Name.Trim();
        product.Slug = slug;
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.BrandId = request.BrandId;
        product.UpdatedAt = DateTime.UtcNow;

        // Synchronize category links.
        var requestedCategoryIds = categoryIds.ToHashSet();

        var linksToRemove = product.ProductCategories
            .Where(link => !requestedCategoryIds.Contains(link.CategoryId))
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

        // Remove specifications omitted from the request.
        var requestedSpecificationNames = specificationNames
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var specificationsToRemove = product.Specifications
            .Where(existing =>
                !requestedSpecificationNames.Contains(existing.Name))
            .ToList();

        _dataContext.ProductSpecifications.RemoveRange(
            specificationsToRemove);

        // Update matching specifications in place; add only genuinely new ones.
        foreach (var specification in specifications)
        {
            var existingSpecification = product.Specifications
                .FirstOrDefault(existing =>
                    string.Equals(
                        existing.Name,
                        specification.Name,
                        StringComparison.OrdinalIgnoreCase));

            if (existingSpecification is not null)
            {
                existingSpecification.Name = specification.Name;
                existingSpecification.Value = specification.Value;
                existingSpecification.SortOrder = specification.SortOrder;

                continue;
            }

            _dataContext.ProductSpecifications.Add(
                new ProductSpecification
                {
                    Id = await GetDbIdentityAsync(),
                    ProductId = product.Id,
                    Name = specification.Name,
                    Value = specification.Value,
                    SortOrder = specification.SortOrder
                });
        }

        await SaveChangesOrThrowConflictAsync();

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
    public async Task<bool> RestoreProductAsync(Guid id)
    {
        var product = await _dataContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsActive);

        if (product is null)
        {
            return false;
        }

        product.IsActive = true;
        product.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync();

        return true;
    }

    public async Task<List<ProductSpecificationDto>?> GetProductSpecificationsAsync(
        Guid productId,
        bool includeInactiveProduct = false)
    {
        var productQuery = _dataContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId);

        if (!includeInactiveProduct)
        {
            productQuery = productQuery.Where(product => product.IsActive);
        }

        if (!await productQuery.AnyAsync())
        {
            return null;
        }

        return await _dataContext.ProductSpecifications
            .AsNoTracking()
            .Where(specification => specification.ProductId == productId)
            .OrderBy(specification => specification.SortOrder)
            .ThenBy(specification => specification.Name)
            .ThenBy(specification => specification.Id)
            .Select(specification => new ProductSpecificationDto
            {
                Id = specification.Id,
                Name = specification.Name,
                Value = specification.Value,
                SortOrder = specification.SortOrder
            })
            .ToListAsync();
    }
    public async Task<ProductSpecificationDto?> GetProductSpecificationAsync(
        Guid productId,
        Guid specificationId,
        bool includeInactiveProduct = false)
    {
        var productQuery = _dataContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId);

        if (!includeInactiveProduct)
        {
            productQuery = productQuery.Where(product => product.IsActive);
        }

        if (!await productQuery.AnyAsync())
        {
            return null;
        }

        return await _dataContext.ProductSpecifications
            .AsNoTracking()
            .Where(specification =>
                specification.ProductId == productId &&
                specification.Id == specificationId)
            .Select(specification => new ProductSpecificationDto
            {
                Id = specification.Id,
                Name = specification.Name,
                Value = specification.Value,
                SortOrder = specification.SortOrder
            })
            .FirstOrDefaultAsync();
    }
    private async Task EnsureSpecificationNameIsUniqueAsync(
        Guid productId,
        string name,
        Guid? excludeSpecificationId = null)
    {
        var query = _dataContext.ProductSpecifications
            .AsNoTracking()
            .Where(specification => specification.ProductId == productId);

        if (excludeSpecificationId.HasValue)
        {
            query = query.Where(specification =>
                specification.Id != excludeSpecificationId.Value);
        }

        var existingNames = await query
            .Select(specification => specification.Name)
            .ToListAsync();

        if (existingNames.Any(existingName =>
            string.Equals(
                existingName.Trim(),
                name,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "A specification with this name already exists for the product.");
        }
    }
    public async Task<ProductSpecificationDto> CreateProductSpecificationAsync(
        Guid productId,
        ProductSpecificationCreateRequest request)
    {
        var product = await _dataContext.Products
            .FirstOrDefaultAsync(product => product.Id == productId);

        if (product is null)
        {
            throw new KeyNotFoundException("Product not found.");
        }

        var (name, value) = ValidateAndNormalizeSpecification(
            request.Name,
            request.Value,
            request.SortOrder);

        await EnsureSpecificationNameIsUniqueAsync(productId, name);

        var specification = new ProductSpecification
        {
            Id = await GetDbIdentityAsync(),
            ProductId = productId,
            Name = name,
            Value = value,
            SortOrder = request.SortOrder
        };

        _dataContext.ProductSpecifications.Add(specification);

        product.UpdatedAt = DateTime.UtcNow;

        await SaveChangesOrThrowConflictAsync();

        return new ProductSpecificationDto
        {
            Id = specification.Id,
            Name = specification.Name,
            Value = specification.Value,
            SortOrder = specification.SortOrder
        };
    }
    public async Task<ProductSpecificationDto?> UpdateProductSpecificationAsync(
        Guid productId,
        Guid specificationId,
        ProductSpecificationUpdateRequest request)
    {
        var product = await _dataContext.Products
            .FirstOrDefaultAsync(product => product.Id == productId);

        if (product is null)
        {
            return null;
        }

        var specification = await _dataContext.ProductSpecifications
            .FirstOrDefaultAsync(specification =>
                specification.Id == specificationId &&
                specification.ProductId == productId);

        if (specification is null)
        {
            return null;
        }

        var (name, value) = ValidateAndNormalizeSpecification(
            request.Name,
            request.Value,
            request.SortOrder);

        await EnsureSpecificationNameIsUniqueAsync(
            productId,
            name,
            specificationId);

        specification.Name = name;
        specification.Value = value;
        specification.SortOrder = request.SortOrder;

        product.UpdatedAt = DateTime.UtcNow;

        await SaveChangesOrThrowConflictAsync();

        return new ProductSpecificationDto
        {
            Id = specification.Id,
            Name = specification.Name,
            Value = specification.Value,
            SortOrder = specification.SortOrder
        };
    }
    public async Task<bool> DeleteProductSpecificationAsync(
        Guid productId,
        Guid specificationId)
    {
        var product = await _dataContext.Products
            .FirstOrDefaultAsync(product => product.Id == productId);

        if (product is null)
        {
            return false;
        }

        var specification = await _dataContext.ProductSpecifications
            .FirstOrDefaultAsync(specification =>
                specification.Id == specificationId &&
                specification.ProductId == productId);

        if (specification is null)
        {
            return false;
        }

        _dataContext.ProductSpecifications.Remove(specification);

        product.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync();

        return true;
    }


    private static (string Name, string Value) ValidateAndNormalizeSpecification(
        string name,
        string value,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Specification name is required.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Specification value is required.",
                nameof(value));
        }

        name = name.Trim();
        value = value.Trim();

        if (name.Length > 150)
        {
            throw new ArgumentException(
                "Specification name cannot exceed 150 characters.",
                nameof(name));
        }

        if (value.Length > 1000)
        {
            throw new ArgumentException(
                "Specification value cannot exceed 1000 characters.",
                nameof(value));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "SortOrder cannot be negative.");
        }

        return (name, value);
    }
    private static string BuildProductImageUrl(string imageKey)
    {
        return $"/api/products/images/{Uri.EscapeDataString(imageKey)}";
    }

    // Categories
    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        var categories = await _dataContext.Categories
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

        foreach (var category in categories)
        {
            category.ImageUrl = BuildCategoryImageUrl(category.ImageUrl);
        }

        return categories;
    }
    public async Task<CategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        var category = await _dataContext.Categories
            .AsNoTracking()
            .Where(category =>
                category.Id == id &&
                category.IsActive)
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

        if (category is not null)
        {
            category.ImageUrl = BuildCategoryImageUrl(category.ImageUrl);
        }

        return category;
    }
    public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug)
    {
        var category = await _dataContext.Categories
            .AsNoTracking()
            .Where(category =>
                category.Slug == slug &&
                category.IsActive)
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

        if (category is not null)
        {
            category.ImageUrl = BuildCategoryImageUrl(category.ImageUrl);
        }

        return category;
    }
    public async Task<bool> CategoryExistsAsync(Guid id)
    {
        return await _dataContext.Categories
            .AsNoTracking()
            .AnyAsync(category => category.Id == id && category.IsActive);
    }
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
            ImageUrl = BuildCategoryImageUrl(category.ImageUrl),
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

        await _dataContext.SaveChangesAsync();

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ImageUrl = BuildCategoryImageUrl(category.ImageUrl),
            ParentCategoryId = category.ParentCategoryId,
            SortOrder = category.SortOrder
        };
    }
    public async Task<(bool Updated, string? PreviousImageKey)> SetCategoryImageAsync(
        Guid categoryId,
        string imageKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageKey))
        {
            throw new ArgumentException(
                "Image key cannot be empty.",
                nameof(imageKey));
        }

        var category = await _dataContext.Categories
            .FirstOrDefaultAsync(
                category =>
                    category.Id == categoryId &&
                    category.IsActive,
                cancellationToken);

        if (category is null)
        {
            return (false, null);
        }

        var previousImageKey = category.ImageUrl;

        category.ImageUrl = imageKey.Trim();

        await _dataContext.SaveChangesAsync(cancellationToken);

        return (true, previousImageKey);
    }
    public async Task<bool> DeleteCategoryAsync(Guid id)
    {
        var category = await _dataContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

        if (category is null)
        {
            return false;
        }

        var hasActiveChildren = await _dataContext.Categories
            .AnyAsync(child =>
                child.ParentCategoryId == id &&
                child.IsActive);

        if (hasActiveChildren)
        {
            throw new InvalidOperationException(
                "Active child categories must be deactivated first.");
        }

        category.IsActive = false;

        await _dataContext.SaveChangesAsync();

        return true;
    }
    public async Task<bool> RestoreCategoryAsync(Guid id)
    {
        var category = await _dataContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsActive);

        if (category is null)
        {
            return false;
        }

        if (category.ParentCategoryId.HasValue)
        {
            var parentIsActive = await _dataContext.Categories
                .AnyAsync(parent =>
                    parent.Id == category.ParentCategoryId.Value &&
                    parent.IsActive);

            if (!parentIsActive)
            {
                throw new InvalidOperationException(
                    "The parent category must be active before restoring this category.");
            }
        }

        category.IsActive = true;

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


    private static string? BuildCategoryImageUrl(string? imageKey)
    {
        if (string.IsNullOrWhiteSpace(imageKey))
        {
            return null;
        }

        // Preserve existing absolute HTTP(S) image URLs.
        if (Uri.TryCreate(imageKey, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps))
        {
            return imageKey;
        }

        return $"/api/categories/images/{Uri.EscapeDataString(imageKey)}";
    }

    // Brands
    public async Task<List<BrandDto>> GetBrandsAsync()
    {
        var brands = await _dataContext.Brands
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

        foreach (var brand in brands)
        {
            brand.LogoUrl = BuildBrandLogoUrl(brand.LogoUrl);
        }

        return brands;
    }
    public async Task<BrandDto?> GetBrandByIdAsync(Guid id)
    {
        var brand = await _dataContext.Brands
            .AsNoTracking()
            .Where(brand =>
                brand.Id == id &&
                brand.IsActive)
            .Select(brand => new BrandDto
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                Description = brand.Description,
                LogoUrl = brand.LogoUrl
            })
            .FirstOrDefaultAsync();

        if (brand is not null)
        {
            brand.LogoUrl = BuildBrandLogoUrl(brand.LogoUrl);
        }

        return brand;
    }
    public async Task<BrandDto?> GetBrandBySlugAsync(string slug)
    {
        var brand = await _dataContext.Brands
            .AsNoTracking()
            .Where(brand =>
                brand.Slug == slug &&
                brand.IsActive)
            .Select(brand => new BrandDto
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                Description = brand.Description,
                LogoUrl = brand.LogoUrl
            })
            .FirstOrDefaultAsync();

        if (brand is not null)
        {
            brand.LogoUrl = BuildBrandLogoUrl(brand.LogoUrl);
        }

        return brand;
    }
    public async Task<bool> BrandExistsAsync(Guid id)
    {
        return await _dataContext.Brands
            .AsNoTracking()
            .AnyAsync(brand => brand.Id == id && brand.IsActive);
    }
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
            LogoUrl = BuildBrandLogoUrl(brand.LogoUrl)
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

        await _dataContext.SaveChangesAsync();

        return new BrandDto
        {
            Id = brand.Id,
            Name = brand.Name,
            Slug = brand.Slug,
            Description = brand.Description,
            LogoUrl = BuildBrandLogoUrl(brand.LogoUrl)
        };
    }
    public async Task<(bool Updated, string? PreviousLogoKey)> SetBrandLogoAsync(
        Guid brandId,
        string logoKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(logoKey))
        {
            throw new ArgumentException(
                "Logo key cannot be empty.",
                nameof(logoKey));
        }

        var brand = await _dataContext.Brands
            .FirstOrDefaultAsync(
                brand =>
                    brand.Id == brandId &&
                    brand.IsActive,
                cancellationToken);

        if (brand is null)
        {
            return (false, null);
        }

        var previousLogoKey = brand.LogoUrl;

        brand.LogoUrl = logoKey.Trim();

        await _dataContext.SaveChangesAsync(cancellationToken);

        return (true, previousLogoKey);
    }
    public async Task<bool> DeleteBrandAsync(Guid id)
    {
        var brand = await _dataContext.Brands
            .FirstOrDefaultAsync(brand =>
                brand.Id == id && brand.IsActive);

        if (brand is null)
        {
            return false;
        }

        brand.IsActive = false;

        await _dataContext.SaveChangesAsync();

        return true;
    }
    public async Task<bool> RestoreBrandAsync(Guid id)
    {
        var brand = await _dataContext.Brands
            .FirstOrDefaultAsync(brand =>
                brand.Id == id && !brand.IsActive);

        if (brand is null)
        {
            return false;
        }

        brand.IsActive = true;

        await _dataContext.SaveChangesAsync();

        return true;
    }


    private static string? BuildBrandLogoUrl(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return null;
        }

        var value = logoUrl.Trim();

        if (IsLocalStorageLogoKey(value))
        {
            return $"/api/brands/images/{value}";
        }

        return value;
    }
    private static bool IsLocalStorageLogoKey(string logoKey)
    {
        if (string.IsNullOrWhiteSpace(logoKey) ||
            Path.IsPathRooted(logoKey) ||
            logoKey.Contains('/') ||
            logoKey.Contains('\\'))
        {
            return false;
        }

        var extension = Path.GetExtension(logoKey).ToLowerInvariant();

        if (extension is not (".jpg" or ".png" or ".webp"))
        {
            return false;
        }

        var fileId = Path.GetFileNameWithoutExtension(logoKey);

        return Guid.TryParseExact(fileId, "N", out _);
    }

    // Promotions
    public async Task<List<PromotionDto>> GetPromotionsAsync()
    {
        var now = DateTime.UtcNow;

        var promotions = await _dataContext.Promotions
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
                CardImageUrl = promotion.CardImageUrl,
                SortOrder = promotion.SortOrder,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                ProductIds = promotion.PromotionProducts
                    .Select(link => link.ProductId)
                    .ToList(),
            })
            .ToListAsync();

        foreach (var promotion in promotions)
        {
            promotion.BannerImageUrl =
                BuildPromotionImageUrl(promotion.BannerImageUrl);

            promotion.CardImageUrl =
                BuildPromotionImageUrl(promotion.CardImageUrl);
        }

        return promotions;
    }
    public async Task<PromotionDetailsDto?> GetPromotionByIdAsync(Guid id)
    {
        var now = DateTime.UtcNow;

        var promotion = await _dataContext.Promotions
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
                CardImageUrl = promotion.CardImageUrl,
                ImageUrl = promotion.ImageUrl,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                ProductIds = promotion.PromotionProducts
                    .Select(link => link.ProductId)
                    .ToList(),
            })
            .FirstOrDefaultAsync();

        if (promotion is not null)
        {
            promotion.BannerImageUrl =
                BuildPromotionImageUrl(promotion.BannerImageUrl);

            promotion.CardImageUrl =
                BuildPromotionImageUrl(promotion.CardImageUrl);

            promotion.ImageUrl =
                BuildPromotionImageUrl(promotion.ImageUrl);
        }

        return promotion;
    }
    public async Task<PromotionDetailsDto?> GetPromotionBySlugAsync(
        string slug)
    {
        var now = DateTime.UtcNow;
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        var promotion = await _dataContext.Promotions
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
                CardImageUrl = promotion.CardImageUrl,
                ImageUrl = promotion.ImageUrl,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                ProductIds = promotion.PromotionProducts
                    .Select(link => link.ProductId)
                    .ToList(),
            })
            .FirstOrDefaultAsync();

        if (promotion is not null)
        {
            promotion.BannerImageUrl =
                BuildPromotionImageUrl(promotion.BannerImageUrl);

            promotion.CardImageUrl =
                BuildPromotionImageUrl(promotion.CardImageUrl);

            promotion.ImageUrl =
                BuildPromotionImageUrl(promotion.ImageUrl);
        }

        return promotion;
    }
    public async Task<List<PromotionAdminDto>> GetPromotionsForAdminAsync()
    {
        var promotions = await _dataContext.Promotions
            .AsNoTracking()
            .OrderBy(promotion => promotion.SortOrder)
            .ThenBy(promotion => promotion.Title)
            .Select(promotion => new PromotionAdminDto
            {
                Id = promotion.Id,
                Title = promotion.Title,
                Slug = promotion.Slug,
                Description = promotion.Description,
                BannerImageUrl = promotion.BannerImageUrl,
                CardImageUrl = promotion.CardImageUrl,
                ImageUrl = promotion.ImageUrl,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate,
                IsActive = promotion.IsActive,
                SortOrder = promotion.SortOrder,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                ProductIds = promotion.PromotionProducts
                    .Select(link => link.ProductId)
                    .ToList(),
                CreatedAt = promotion.CreatedAt,
                UpdatedAt = promotion.UpdatedAt
            })
            .ToListAsync();

        foreach (var promotion in promotions)
        {
            promotion.BannerImageUrl =
                BuildPromotionImageUrl(promotion.BannerImageUrl);

            promotion.CardImageUrl =
                BuildPromotionImageUrl(promotion.CardImageUrl);

            promotion.ImageUrl =
                BuildPromotionImageUrl(promotion.ImageUrl);
        }

        return promotions;
    }
    public async Task<PromotionAdminDto?> GetPromotionForAdminByIdAsync(Guid id)
    {
        var promotion = await _dataContext.Promotions
            .AsNoTracking()
            .Where(promotion => promotion.Id == id)
            .Select(promotion => new PromotionAdminDto
            {
                Id = promotion.Id,
                Title = promotion.Title,
                Slug = promotion.Slug,
                Description = promotion.Description,
                BannerImageUrl = promotion.BannerImageUrl,
                CardImageUrl = promotion.CardImageUrl,
                ImageUrl = promotion.ImageUrl,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate,
                IsActive = promotion.IsActive,
                SortOrder = promotion.SortOrder,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                ProductIds = promotion.PromotionProducts
                    .Select(link => link.ProductId)
                    .ToList(),
                CreatedAt = promotion.CreatedAt,
                UpdatedAt = promotion.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (promotion is not null)
        {
            promotion.BannerImageUrl =
                BuildPromotionImageUrl(promotion.BannerImageUrl);

            promotion.CardImageUrl =
                BuildPromotionImageUrl(promotion.CardImageUrl);

            promotion.ImageUrl =
                BuildPromotionImageUrl(promotion.ImageUrl);
        }

        return promotion;
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
        ValidatePromotionDates(
            request.StartDate,
            request.EndDate);

        ValidatePromotionDiscount(
            request.DiscountType,
            request.DiscountValue);

        var productIds = await ValidatePromotionProductIdsAsync(
            request.ProductIds);

        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await PromotionSlugExistsAsync(slug))
        {
            throw new InvalidOperationException(
                "A promotion with this slug already exists.");
        }

        var promotion = new Promotion
        {
            Title = request.Title.Trim(),
            Slug = slug,
            Description = request.Description?.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            CreatedAt = DateTime.UtcNow
        };

        _dataContext.Promotions.Add(promotion);

        foreach (var productId in productIds)
        {
            _dataContext.PromotionProducts.Add(
                new PromotionProduct
                {
                    PromotionId = promotion.Id,
                    ProductId = productId
                });
        }

        await _dataContext.SaveChangesAsync();

        return await GetPromotionForAdminByIdAsync(promotion.Id)
            ?? throw new InvalidOperationException(
                "The promotion was created but could not be retrieved.");
    }
    public async Task<PromotionAdminDto?> UpdatePromotionAsync(
        Guid id,
        PromotionUpdateRequest request)
    {
        ValidatePromotionDates(
            request.StartDate,
            request.EndDate);

        ValidatePromotionDiscount(
            request.DiscountType,
            request.DiscountValue);

        var isActive = request.IsActive
            ?? throw new ArgumentException(
                "IsActive is required.");

        var promotion = await _dataContext.Promotions
            .FirstOrDefaultAsync(promotion => promotion.Id == id);

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

        var productIds = await ValidatePromotionProductIdsAsync(
            request.ProductIds);

        promotion.Title = request.Title.Trim();
        promotion.Slug = slug;
        promotion.Description = request.Description?.Trim();
        promotion.StartDate = request.StartDate;
        promotion.EndDate = request.EndDate;
        promotion.IsActive = isActive;
        promotion.SortOrder = request.SortOrder;
        promotion.DiscountType = request.DiscountType;
        promotion.DiscountValue = request.DiscountValue;
        promotion.UpdatedAt = DateTime.UtcNow;

        var existingLinks = await _dataContext.PromotionProducts
            .Where(link => link.PromotionId == id)
            .ToListAsync();

        var requestedProductIds = productIds.ToHashSet();

        var linksToRemove = existingLinks
            .Where(link => !requestedProductIds.Contains(link.ProductId))
            .ToList();

        _dataContext.PromotionProducts.RemoveRange(linksToRemove);

        var existingProductIds = existingLinks
            .Select(link => link.ProductId)
            .ToHashSet();

        foreach (var productId in productIds.Where(
            productId => !existingProductIds.Contains(productId)))
        {
            _dataContext.PromotionProducts.Add(
                new PromotionProduct
                {
                    PromotionId = promotion.Id,
                    ProductId = productId
                });
        }

        await _dataContext.SaveChangesAsync();

        return await GetPromotionForAdminByIdAsync(id);
    }
    public async Task<(bool Updated, string? PreviousImageKey)> SetPromotionImageAsync(
        Guid promotionId,
        string imageKey,
        PromotionImageType imageType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageKey))
        {
            throw new ArgumentException(
                "Image key cannot be empty.",
                nameof(imageKey));
        }

        var promotion = await _dataContext.Promotions
            .FirstOrDefaultAsync(
                promotion => promotion.Id == promotionId,
                cancellationToken);

        if (promotion is null)
        {
            return (false, null);
        }

        var previousImageKey = imageType switch
        {
            PromotionImageType.Banner => promotion.BannerImageUrl,
            PromotionImageType.Card => promotion.CardImageUrl,
            PromotionImageType.Details => promotion.ImageUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(imageType))
        };

        switch (imageType)
        {
            case PromotionImageType.Banner:
                promotion.BannerImageUrl = imageKey.Trim();
                break;

            case PromotionImageType.Card:
                promotion.CardImageUrl = imageKey.Trim();
                break;

            case PromotionImageType.Details:
                promotion.ImageUrl = imageKey.Trim();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(imageType));
        }

        promotion.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        return (true, previousImageKey);
    }
    public async Task<bool> DeletePromotionAsync(Guid id)
    {
        var promotion = await _dataContext.Promotions
            .FirstOrDefaultAsync(p =>
                p.Id == id && p.IsActive);

        if (promotion is null)
        {
            return false;
        }

        promotion.IsActive = false;
        promotion.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync();

        return true;
    }
    public async Task<bool> RestorePromotionAsync(Guid id)
    {
        var promotion = await _dataContext.Promotions
            .FirstOrDefaultAsync(p =>
                p.Id == id && !p.IsActive);

        if (promotion is null)
        {
            return false;
        }

        promotion.IsActive = true;
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
    private static void ValidatePromotionDiscount(
    PromotionDiscountType? discountType,
    decimal? discountValue)
    {
        if (!discountType.HasValue && !discountValue.HasValue)
        {
            return;
        }

        if (!discountType.HasValue || !discountValue.HasValue)
        {
            throw new ArgumentException(
                "DiscountType and DiscountValue must be provided together.");
        }

        if (!Enum.IsDefined(discountType.Value))
        {
            throw new ArgumentException(
                "The specified discount type is invalid.");
        }

        if (decimal.Round(discountValue.Value, 2) != discountValue.Value)
        {
            throw new ArgumentException(
                "DiscountValue cannot have more than two decimal places.");
        }

        switch (discountType.Value)
        {
            case PromotionDiscountType.Percentage:
                if (discountValue.Value <= 0 ||
                    discountValue.Value > 100)
                {
                    throw new ArgumentException(
                        "Percentage discount must be greater than 0 and at most 100.");
                }

                break;

            case PromotionDiscountType.FixedAmount:
                if (discountValue.Value <= 0)
                {
                    throw new ArgumentException(
                        "Fixed amount discount must be greater than 0.");
                }

                break;
        }
    }
    private async Task<List<Guid>> ValidatePromotionProductIdsAsync(
        IEnumerable<Guid>? requestedProductIds)
    {
        if (requestedProductIds is null)
        {
            throw new ArgumentException(
                "ProductIds cannot be null.");
        }

        var productIds = requestedProductIds.ToList();

        if (productIds.Distinct().Count() != productIds.Count)
        {
            throw new ArgumentException(
                "Duplicate product IDs are not allowed.");
        }

        if (productIds.Count == 0)
        {
            return productIds;
        }

        var activeProductCount = await _dataContext.Products
            .CountAsync(product =>
                productIds.Contains(product.Id) &&
                product.IsActive);

        if (activeProductCount != productIds.Count)
        {
            throw new ArgumentException(
                "One or more products do not exist or are inactive.");
        }

        return productIds;
    }
    private sealed record PromotionDiscountCandidate(
        Guid ProductId,
        Guid PromotionId,
        string Title,
        string Slug,
        PromotionDiscountType DiscountType,
        decimal DiscountValue,
        int SortOrder);
    private async Task<Dictionary<Guid, List<PromotionDiscountCandidate>>> GetActivePromotionDiscountsAsync(
            IEnumerable<Guid> productIds)
    {
        var ids = productIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, List<PromotionDiscountCandidate>>();
        }

        var now = DateTime.UtcNow;

        var promotions = await _dataContext.PromotionProducts
            .AsNoTracking()
            .Where(link =>
                ids.Contains(link.ProductId) &&
                link.Promotion.IsActive &&
                link.Promotion.DiscountType.HasValue &&
                link.Promotion.DiscountValue.HasValue &&
                (!link.Promotion.StartDate.HasValue ||
                 link.Promotion.StartDate.Value <= now) &&
                (!link.Promotion.EndDate.HasValue ||
                 link.Promotion.EndDate.Value >= now))
            .Select(link => new
            {
                link.ProductId,
                PromotionId = link.Promotion.Id,
                Title = link.Promotion.Title,
                Slug = link.Promotion.Slug,
                DiscountType = link.Promotion.DiscountType,
                DiscountValue = link.Promotion.DiscountValue,
                SortOrder = link.Promotion.SortOrder
            })
            .ToListAsync();

        return promotions
            .Select(promotion => new PromotionDiscountCandidate(
                promotion.ProductId,
                promotion.PromotionId,
                promotion.Title,
                promotion.Slug,
                promotion.DiscountType!.Value,
                promotion.DiscountValue!.Value,
                promotion.SortOrder))
            .GroupBy(promotion => promotion.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList());
    }
    private static ProductPromotionPriceDto? CalculateBestPromotionPrice(
        decimal originalPrice,
        IEnumerable<PromotionDiscountCandidate>? candidates)
    {
        if (candidates is null)
        {
            return null;
        }

        var options = candidates.Select(candidate =>
        {
            var calculatedDiscount = candidate.DiscountType switch
            {
                PromotionDiscountType.Percentage =>
                    decimal.Round(
                        originalPrice * candidate.DiscountValue / 100m,
                        2,
                        MidpointRounding.AwayFromZero),

                PromotionDiscountType.FixedAmount =>
                    candidate.DiscountValue,

                _ => 0m
            };

            var discountAmount = Math.Min(
                originalPrice,
                calculatedDiscount);

            var discountedPrice = Math.Max(
                0m,
                originalPrice - discountAmount);

            return new
            {
                Candidate = candidate,
                Result = new ProductPromotionPriceDto
                {
                    PromotionId = candidate.PromotionId,
                    Title = candidate.Title,
                    Slug = candidate.Slug,
                    DiscountType = candidate.DiscountType.ToString(),
                    DiscountValue = candidate.DiscountValue,
                    OriginalPrice = originalPrice,
                    DiscountAmount = discountAmount,
                    DiscountedPrice = discountedPrice
                }
            };
        });

        return options
            .OrderBy(option => option.Result.DiscountedPrice)
            .ThenBy(option => option.Candidate.SortOrder)
            .ThenBy(
                option => option.Candidate.Title,
                StringComparer.OrdinalIgnoreCase)
            .Select(option => option.Result)
            .FirstOrDefault();
    }
    private static string? BuildPromotionImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        var value = imageUrl.Trim();

        if (IsLocalStoragePromotionImageKey(value))
        {
            return $"/api/promotions/images/{value}";
        }

        return value;
    }
    private static bool IsLocalStoragePromotionImageKey(string imageKey)
    {
        if (string.IsNullOrWhiteSpace(imageKey) ||
            Path.IsPathRooted(imageKey) ||
            imageKey.Contains('/') ||
            imageKey.Contains('\\'))
        {
            return false;
        }

        var extension = Path.GetExtension(imageKey).ToLowerInvariant();

        if (extension is not (".jpg" or ".png" or ".webp"))
        {
            return false;
        }

        var fileId = Path.GetFileNameWithoutExtension(imageKey);

        return Guid.TryParseExact(fileId, "N", out _);
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

        var isAdmin = await _userManager.IsInRoleAsync(
            user,
            AppRoles.Admin);

        if (isAdmin)
        {
            var administrators = await _userManager.GetUsersInRoleAsync(
                AppRoles.Admin);

            if (administrators.Count <= 1)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "LastAdmin",
                    Description = "The last administrator cannot be deleted."
                });
            }
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



    private async Task SaveChangesOrThrowConflictAsync()
    {
        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
            when (IsDuplicateKeyViolation(exception))
        {
            throw new InvalidOperationException(
                "A product or one of its related records conflicts " +
                "with an existing unique value.",
                exception);
        }
    }

    private static bool IsDuplicateKeyViolation(
        DbUpdateException exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is MySqlException mysqlException
                && mysqlException.Number == 1062)
            {
                return true;
            }
        }

        return false;
    }

}

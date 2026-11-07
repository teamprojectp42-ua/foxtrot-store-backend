using foxtrot_store_backend.Constants;
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Auth;
using foxtrot_store_backend.Models.Brands;
using foxtrot_store_backend.Models.Cart;
using foxtrot_store_backend.Models.Categories;
using foxtrot_store_backend.Models.Orders;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Promotions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using System.Globalization;
using foxtrot_store_backend.Models.Products.Translations;

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
    public async Task<List<ProductImageDto>?> GetProductImagesAsync(
        Guid productId,
        bool includeInactiveProduct = false,
        CancellationToken cancellationToken = default)
    {
        var productQuery = _dataContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId);

        if (!includeInactiveProduct)
        {
            productQuery = productQuery.Where(product => product.IsActive);
        }

        if (!await productQuery.AnyAsync(cancellationToken))
        {
            return null;
        }

        var images = await _dataContext.ProductImages
            .AsNoTracking()
            .Where(image => image.ProductId == productId)
            .OrderByDescending(image => image.IsPrimary)
            .ThenBy(image => image.SortOrder)
            .ThenBy(image => image.Id)
            .Select(image => new ProductImageDto
            {
                Id = image.Id,
                ImageUrl = image.ImageUrl,
                IsPrimary = image.IsPrimary,
                SortOrder = image.SortOrder
            })
            .ToListAsync(cancellationToken);

        foreach (var image in images)
        {
            image.ImageUrl = BuildProductImageUrl(image.ImageUrl);
        }

        return images;
    }
    public async Task<ProductImageDto?> AddProductImageAsync(
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
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
        {
            return null;
        }

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

        return new ProductImageDto
        {
            Id = image.Id,
            ImageUrl = BuildProductImageUrl(image.ImageUrl),
            IsPrimary = image.IsPrimary,
            SortOrder = image.SortOrder
        };
    }
    public async Task<ProductImageDto?> UpdateProductImageAsync(
        Guid productId,
        Guid imageId,
        ProductImageUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsPrimary.HasValue)
        {
            throw new ArgumentException(
                "IsPrimary is required.",
                nameof(request));
        }

        if (!request.SortOrder.HasValue || request.SortOrder.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "SortOrder must be zero or greater.");
        }

        var product = await _dataContext.Products
            .Include(product => product.Images)
            .FirstOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
        {
            return null;
        }

        var image = product.Images
            .FirstOrDefault(image => image.Id == imageId);

        if (image is null)
        {
            return null;
        }

        if (request.IsPrimary.Value)
        {
            foreach (var otherImage in product.Images)
            {
                otherImage.IsPrimary = otherImage.Id == imageId;
            }
        }
        else
        {
            image.IsPrimary = false;
        }

        image.SortOrder = request.SortOrder.Value;
        product.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        return new ProductImageDto
        {
            Id = image.Id,
            ImageUrl = BuildProductImageUrl(image.ImageUrl),
            IsPrimary = image.IsPrimary,
            SortOrder = image.SortOrder
        };
    }
    public async Task<(bool Deleted, string? ImageKey)> DeleteProductImageAsync(
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var product = await _dataContext.Products
            .Include(product => product.Images)
            .FirstOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
        {
            return (false, null);
        }

        var image = product.Images
            .FirstOrDefault(image => image.Id == imageId);

        if (image is null)
        {
            return (false, null);
        }

        var imageKey = image.ImageUrl;
        var wasPrimary = image.IsPrimary;

        _dataContext.ProductImages.Remove(image);

        if (wasPrimary)
        {
            var remainingImages = product.Images
                .Where(otherImage => otherImage.Id != imageId)
                .OrderBy(otherImage => otherImage.SortOrder)
                .ThenBy(otherImage => otherImage.Id)
                .ToList();

            foreach (var remainingImage in remainingImages)
            {
                remainingImage.IsPrimary = false;
            }

            if (remainingImages.Count > 0)
            {
                remainingImages[0].IsPrimary = true;
            }
        }

        product.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        return (true, imageKey);
    }
    public async Task<ProductCatalogResponseDto> GetProductsAsync(
        ProductCatalogQuery query,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? "name"
            : query.SortBy.Trim().ToLowerInvariant();

        var sortDirection = string.IsNullOrWhiteSpace(query.SortDirection)
            ? "asc"
            : query.SortDirection.Trim().ToLowerInvariant();

        if (query.MinPrice.HasValue &&
            query.MaxPrice.HasValue &&
            query.MinPrice.Value > query.MaxPrice.Value)
        {
            throw new ArgumentException(
                "MinPrice cannot be greater than MaxPrice.");
        }

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
                product.Sku.Contains(search) ||

                product.Translations.Any(translation =>
                    translation.CultureCode == cultureCode &&
                    (
                        translation.Name.Contains(search) ||
                        translation.Description.Contains(search)
                    )) ||

                product.Translations.Any(translation =>
                    translation.CultureCode == DefaultCultureCode &&
                    (
                        translation.Name.Contains(search) ||
                        translation.Description.Contains(search)
                    )));
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

        products = (sortBy, sortDirection) switch
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
                .OrderByDescending(product =>
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    string.Empty)
                .ThenBy(product => product.Id),

            _ => products
                .OrderBy(product =>
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    string.Empty)
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

                Name =
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    string.Empty,

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
    public async Task<ProductDetailsDto?> GetProductByIdAsync(
        Guid id,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        var product = await _dataContext.Products
            .AsNoTracking()
            .Where(product =>
                product.Id == id &&
                product.IsActive)
            .Select(product => new ProductDetailsDto
            {
                Id = product.Id,
                Sku = product.Sku,

                Name =
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    string.Empty,

                Slug = product.Slug,

                Description =
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Description)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Description)
                        .FirstOrDefault()
                    ??
                    string.Empty,

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
                    .ThenBy(image => image.Id)
                    .Select(image => new ProductImageDto
                    {
                        Id = image.Id,
                        ImageUrl = image.ImageUrl,
                        IsPrimary = image.IsPrimary,
                        SortOrder = image.SortOrder
                    })
                    .ToList(),


                Specifications = product.Specifications
                    .OrderBy(specification => specification.SortOrder)
                    .ThenBy(specification => specification.Id)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Id = specification.Id,

                        Name =
                            specification.Translations
                                .Where(t => t.CultureCode == cultureCode)
                                .Select(t => t.Name)
                                .FirstOrDefault()
                            ??
                            specification.Translations
                                .Where(t => t.CultureCode == DefaultCultureCode)
                                .Select(t => t.Name)
                                .FirstOrDefault()
                            ??
                            string.Empty,

                        Value =
                            specification.Translations
                                .Where(t => t.CultureCode == cultureCode)
                                .Select(t => t.Value)
                                .FirstOrDefault()
                            ??
                            specification.Translations
                                .Where(t => t.CultureCode == DefaultCultureCode)
                                .Select(t => t.Value)
                                .FirstOrDefault()
                            ??
                            string.Empty,

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
        string slug,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        slug = slug.Trim().ToLowerInvariant();

        var product = await _dataContext.Products
            .AsNoTracking()
            .Where(product =>
                product.Slug == slug &&
                product.IsActive)
            .Select(product => new ProductDetailsDto
            {
                Id = product.Id,
                Sku = product.Sku,

                Name =
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    string.Empty,

                Description =
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Description)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Description)
                        .FirstOrDefault()
                    ??
                    string.Empty,

                Slug = product.Slug,
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
                    .ThenBy(image => image.Id)
                    .Select(image => new ProductImageDto
                    {
                        Id = image.Id,
                        ImageUrl = image.ImageUrl,
                        IsPrimary = image.IsPrimary,
                        SortOrder = image.SortOrder
                    })
                    .ToList(),


                Specifications = product.Specifications
                    .OrderBy(specification => specification.SortOrder)
                    .ThenBy(specification => specification.Id)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Id = specification.Id,

                        Name =
                            specification.Translations
                                .Where(t => t.CultureCode == cultureCode)
                                .Select(t => t.Name)
                                .FirstOrDefault()
                            ??
                            specification.Translations
                                .Where(t => t.CultureCode == DefaultCultureCode)
                                .Select(t => t.Name)
                                .FirstOrDefault()
                            ??
                            string.Empty,

                        Value =
                            specification.Translations
                                .Where(t => t.CultureCode == cultureCode)
                                .Select(t => t.Value)
                                .FirstOrDefault()
                            ??
                            specification.Translations
                                .Where(t => t.CultureCode == DefaultCultureCode)
                                .Select(t => t.Value)
                                .FirstOrDefault()
                            ??
                            string.Empty,

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
        ProductCreateRequest request,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        // Validate product translations.
        var translations = NormalizeProductTranslations(
            request.Translations);

        var sku = request.Sku.Trim();
        var slug = request.Slug.Trim().ToLowerInvariant();

        // Check for duplicate SKU.
        if (await _dataContext.Products.AnyAsync(
                product => product.Sku == sku))
        {
            throw new InvalidOperationException(
                "A product with this SKU already exists.");
        }

        // Check for duplicate slug.
        if (await _dataContext.Products.AnyAsync(
                product => product.Slug == slug))
        {
            throw new InvalidOperationException(
                "A product with this slug already exists.");
        }

        // Validate brand.
        if (request.BrandId.HasValue)
        {
            var brandExists = await _dataContext.Brands.AnyAsync(
                brand =>
                    brand.Id == request.BrandId.Value &&
                    brand.IsActive);

            if (!brandExists)
            {
                throw new ArgumentException(
                    "The specified brand does not exist or is inactive.");
            }
        }

        // Validate categories.
        var categoryIds = request.CategoryIds
            .Distinct()
            .ToList();

        if (categoryIds.Count != request.CategoryIds.Count)
        {
            throw new ArgumentException(
                "Duplicate category IDs are not allowed.");
        }

        if (categoryIds.Count > 0)
        {
            var activeCategoryCount =
                await _dataContext.Categories.CountAsync(
                    category =>
                        categoryIds.Contains(category.Id) &&
                        category.IsActive);

            if (activeCategoryCount != categoryIds.Count)
            {
                throw new ArgumentException(
                    "One or more categories do not exist or are inactive.");
            }
        }

        // Validate specifications.
        if (request.Specifications is null)
        {
            throw new ArgumentException(
                "Specifications cannot be null.");
        }

        var specifications = request.Specifications
            .Select(specification =>
            {
                if (specification is null)
                {
                    throw new ArgumentException(
                        "Specification cannot be null.");
                }

                var specificationTranslations =
                    NormalizeSpecificationTranslations(
                        specification.Translations);

                if (specification.SortOrder < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(specification.SortOrder),
                        "SortOrder cannot be negative.");
                }

                return new
                {
                    Translations = specificationTranslations,
                    specification.SortOrder
                };
            })
            .ToList();

        EnsureUniqueSpecificationNames(
            specifications.SelectMany(
                specification => specification.Translations));


        // Names must be unique within each culture.
        EnsureUniqueSpecificationNames(
            specifications.SelectMany(
                specification => specification.Translations));

        var now = DateTime.UtcNow;
        var productId = await GetDbIdentityAsync();

        var product = new Product
        {
            Id = productId,
            Sku = sku,
            Slug = slug,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            BrandId = request.BrandId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dataContext.Products.Add(product);

        // Save all product translations.
        await SyncProductTranslationsAsync(
            product,
            translations);

        // Add product categories.
        foreach (var categoryId in categoryIds)
        {
            _dataContext.ProductCategories.Add(
                new ProductCategory
                {
                    ProductId = product.Id,
                    CategoryId = categoryId
                });
        }

        // Add specifications and their translations.
        foreach (var specification in specifications)
        {
            var entity = new ProductSpecification
            {
                Id = await GetDbIdentityAsync(),
                ProductId = product.Id,
                SortOrder = specification.SortOrder
            };

            _dataContext.ProductSpecifications.Add(entity);

            await SyncSpecificationTranslationsAsync(
                entity,
                product.Id,
                specification.Translations);
        }

        await SaveChangesOrThrowConflictAsync();

        var createdProduct = await GetProductForAdminByIdAsync(
            product.Id,
            cultureCode);

        return createdProduct
            ?? throw new InvalidOperationException(
                "The product was created but could not be retrieved.");
    }
    public async Task<ProductDetailsDto?> GetProductForAdminByIdAsync(
        Guid id,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        var product = await _dataContext.Products
            .AsNoTracking()
            .Where(product => product.Id == id)
            .Select(product => new ProductDetailsDto
            {
                Id = product.Id,
                Sku = product.Sku,

                Translations = product.Translations
                    .OrderBy(translation => translation.CultureCode)
                    .Select(translation => new ProductTranslationDto
                    {
                        CultureCode = translation.CultureCode,
                        Name = translation.Name,
                        Description = translation.Description
                    })
                    .ToList(),

                Name =
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Name)
                        .FirstOrDefault()
                    ??
                    string.Empty,

                Description =
                    product.Translations
                        .Where(t => t.CultureCode == cultureCode)
                        .Select(t => t.Description)
                        .FirstOrDefault()
                    ??
                    product.Translations
                        .Where(t => t.CultureCode == DefaultCultureCode)
                        .Select(t => t.Description)
                        .FirstOrDefault()
                    ??
                    string.Empty,

                Slug = product.Slug,
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
                    .ThenBy(image => image.Id)
                    .Select(image => new ProductImageDto
                    {
                        Id = image.Id,
                        ImageUrl = image.ImageUrl,
                        IsPrimary = image.IsPrimary,
                        SortOrder = image.SortOrder
                    })
                    .ToList(),


                Specifications = product.Specifications
                    .OrderBy(specification => specification.SortOrder)
                    .ThenBy(specification => specification.Id)
                    .Select(specification => new ProductSpecificationDto
                    {
                        Id = specification.Id,

                        Translations = specification.Translations
                            .OrderBy(translation => translation.CultureCode)
                            .Select(translation =>
                                new ProductSpecificationTranslationDto
                                {
                                    CultureCode = translation.CultureCode,
                                    Name = translation.Name,
                                    Value = translation.Value
                                })
                            .ToList(),

                        Name =
                            specification.Translations
                                .Where(t => t.CultureCode == cultureCode)
                                .Select(t => t.Name)
                                .FirstOrDefault()
                            ??
                            specification.Translations
                                .Where(t => t.CultureCode == DefaultCultureCode)
                                .Select(t => t.Name)
                                .FirstOrDefault()
                            ??
                            string.Empty,

                        Value =
                            specification.Translations
                                .Where(t => t.CultureCode == cultureCode)
                                .Select(t => t.Value)
                                .FirstOrDefault()
                            ??
                            specification.Translations
                                .Where(t => t.CultureCode == DefaultCultureCode)
                                .Select(t => t.Value)
                                .FirstOrDefault()
                            ??
                            string.Empty,

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
        ProductUpdateRequest request,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        var product = await _dataContext.Products
            .Include(p => p.Translations)
            .Include(p => p.ProductCategories)
            .Include(p => p.Specifications)
                .ThenInclude(s => s.Translations)
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
                brand =>
                    brand.Id == request.BrandId.Value &&
                    brand.IsActive);

            if (!brandExists)
            {
                throw new ArgumentException(
                    "The specified brand does not exist or is inactive.");
            }
        }

        var categoryIds = request.CategoryIds
            .Distinct()
            .ToList();

        if (categoryIds.Count != request.CategoryIds.Count)
        {
            throw new ArgumentException(
                "Duplicate category IDs are not allowed.");
        }

        if (categoryIds.Count > 0)
        {
            var activeCategoryCount =
                await _dataContext.Categories.CountAsync(
                    category =>
                        categoryIds.Contains(category.Id) &&
                        category.IsActive);

            if (activeCategoryCount != categoryIds.Count)
            {
                throw new ArgumentException(
                    "One or more categories do not exist or are inactive.");
            }
        }

        if (request.Specifications is null)
        {
            throw new ArgumentException(
                "Specifications cannot be null.");
        }

        var translations = NormalizeProductTranslations(
            request.Translations);

        var specifications = request.Specifications
            .Select(specification =>
            {
                if (specification is null)
                {
                    throw new ArgumentException(
                        "Specification cannot be null.");
                }

                var specificationTranslations =
                    NormalizeSpecificationTranslations(
                        specification.Translations);

                if (specification.SortOrder < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(specification.SortOrder),
                        "SortOrder cannot be negative.");
                }

                return new
                {
                    specification.Id,
                    Translations = specificationTranslations,
                    specification.SortOrder
                };
            })
            .ToList();

        // Проверяем уникальность названий характеристик
        // во всём входящем запросе отдельно для каждой локали.
        EnsureUniqueSpecificationNames(
            specifications.SelectMany(s => s.Translations));

        var existingSpecifications = product.Specifications
            .ToDictionary(s => s.Id);

        var processedSpecificationIds = new HashSet<Guid>();

        // Обновляем общие поля товара.
        product.Sku = sku;
        product.Slug = slug;
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.BrandId = request.BrandId;
        product.UpdatedAt = DateTime.UtcNow;

        // Обновляем переводы товара.
        await SyncProductTranslationsAsync(
            product,
            translations);

        // Синхронизируем категории.
        var categoriesToRemove = product.ProductCategories
            .Where(pc => !categoryIds.Contains(pc.CategoryId))
            .ToList();

        _dataContext.ProductCategories.RemoveRange(categoriesToRemove);

        var existingCategoryIds = product.ProductCategories
            .Select(pc => pc.CategoryId)
            .ToHashSet();

        foreach (var categoryId in categoryIds)
        {
            if (existingCategoryIds.Contains(categoryId))
            {
                continue;
            }

            product.ProductCategories.Add(new ProductCategory
            {
                ProductId = product.Id,
                CategoryId = categoryId
            });
        }

        // Обновляем или создаём характеристики.
        foreach (var requested in specifications)
        {
            ProductSpecification specification;

            if (requested.Id.HasValue)
            {
                if (!existingSpecifications.TryGetValue(
                        requested.Id.Value,
                        out specification!))
                {
                    throw new ArgumentException(
                        "The specified characteristic does not belong to this product.");
                }

                if (!processedSpecificationIds.Add(specification.Id))
                {
                    throw new ArgumentException(
                        "A specification was supplied more than once.");
                }
            }
            else
            {
                specification = new ProductSpecification
                {
                    Id = await GetDbIdentityAsync(),
                    ProductId = product.Id
                };

                product.Specifications.Add(specification);

                _dataContext.ProductSpecifications.Add(specification);

                processedSpecificationIds.Add(specification.Id);
            }

            specification.SortOrder = requested.SortOrder;

            await SyncSpecificationTranslationsAsync(
                specification,
                product.Id,
                requested.Translations);
        }

        // Удаляем характеристики, которых больше нет в запросе.
        var specificationsToRemove = product.Specifications
            .Where(s => !processedSpecificationIds.Contains(s.Id))
            .ToList();

        _dataContext.ProductSpecifications.RemoveRange(
            specificationsToRemove);

        await SaveChangesOrThrowConflictAsync();

        return await GetProductForAdminByIdAsync(
            product.Id,
            cultureCode)
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
        string cultureCode = DefaultCultureCode,
        bool includeInactiveProduct = false,
        bool includeTranslations = false)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

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

        var specifications = await _dataContext.ProductSpecifications
            .AsNoTracking()
            .Include(specification => specification.Translations)
            .Where(specification => specification.ProductId == productId)
            .ToListAsync();

        return specifications
            .Select(specification =>
                MapSpecificationDto(
                    specification,
                    cultureCode,
                    includeTranslations))
            .OrderBy(specification => specification.SortOrder)
            .ThenBy(specification => specification.Id)
            .ToList();
    }
    public async Task<ProductSpecificationDto?> GetProductSpecificationAsync(
        Guid productId,
        Guid specificationId,
        string cultureCode = DefaultCultureCode,
        bool includeInactiveProduct = false,
        bool includeTranslations = false)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

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

        var specification = await _dataContext.ProductSpecifications
            .AsNoTracking()
            .Include(specification => specification.Translations)
            .FirstOrDefaultAsync(specification =>
                specification.Id == specificationId &&
                specification.ProductId == productId);

        if (specification is null)
        {
            return null;
        }

        return MapSpecificationDto(
            specification,
            cultureCode,
            includeTranslations);
    }
    public async Task<ProductSpecificationDto> CreateProductSpecificationAsync(
        Guid productId,
        ProductSpecificationCreateRequest request,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        var product = await _dataContext.Products
            .FirstOrDefaultAsync(product => product.Id == productId);

        if (product is null)
        {
            throw new KeyNotFoundException("Product not found.");
        }

        var translations = NormalizeSpecificationTranslations(
            request.Translations);

        if (request.SortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.SortOrder),
                "SortOrder cannot be negative.");
        }

        await EnsureSpecificationTranslationsAreUniqueAsync(
            productId,
            translations);

        var specification = new ProductSpecification
        {
            Id = await GetDbIdentityAsync(),
            ProductId = productId,
            SortOrder = request.SortOrder
        };

        _dataContext.ProductSpecifications.Add(specification);

        await SyncSpecificationTranslationsAsync(
            specification,
            productId,
            translations);

        product.UpdatedAt = DateTime.UtcNow;

        await SaveChangesOrThrowConflictAsync();

        return MapSpecificationDto(
            specification,
            cultureCode,
            includeTranslations: true);
    }
    public async Task<ProductSpecificationDto?> UpdateProductSpecificationAsync(
        Guid productId,
        Guid specificationId,
        ProductSpecificationUpdateRequest request,
        string cultureCode = DefaultCultureCode)
    {
        cultureCode = NormalizeCultureCode(cultureCode);

        var product = await _dataContext.Products
            .FirstOrDefaultAsync(product => product.Id == productId);

        if (product is null)
        {
            return null;
        }

        var specification = await _dataContext.ProductSpecifications
            .Include(s => s.Translations)
            .FirstOrDefaultAsync(s =>
                s.Id == specificationId &&
                s.ProductId == productId);

        if (specification is null)
        {
            return null;
        }

        var translations = NormalizeSpecificationTranslations(
            request.Translations);

        if (request.SortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.SortOrder),
                "SortOrder cannot be negative.");
        }

        await EnsureSpecificationTranslationsAreUniqueAsync(
            productId,
            translations,
            specificationId);

        specification.SortOrder = request.SortOrder;

        await SyncSpecificationTranslationsAsync(
            specification,
            productId,
            translations);

        product.UpdatedAt = DateTime.UtcNow;

        await SaveChangesOrThrowConflictAsync();

        return MapSpecificationDto(
            specification,
            cultureCode,
            includeTranslations: true);
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
            .Include(specification => specification.Translations)
            .FirstOrDefaultAsync(specification =>
                specification.Id == specificationId &&
                specification.ProductId == productId);

        if (specification is null)
        {
            return false;
        }

        _dataContext.ProductSpecificationTranslations.RemoveRange(
            specification.Translations);

        _dataContext.ProductSpecifications.Remove(specification);

        product.UpdatedAt = DateTime.UtcNow;

        await SaveChangesOrThrowConflictAsync();

        return true;
    }


    private static ProductSpecificationDto MapSpecificationDto(
        ProductSpecification specification,
        string cultureCode,
        bool includeTranslations = false)
    {
        var localizedTranslation =
            specification.Translations.FirstOrDefault(
                translation =>
                    translation.CultureCode == cultureCode)
            ??
            specification.Translations.FirstOrDefault(
                translation =>
                    translation.CultureCode == DefaultCultureCode);

        return new ProductSpecificationDto
        {
            Id = specification.Id,

            Name = localizedTranslation?.Name ?? string.Empty,
            Value = localizedTranslation?.Value ?? string.Empty,

            SortOrder = specification.SortOrder,

            Translations = includeTranslations
                ? specification.Translations
                    .OrderBy(translation => translation.CultureCode)
                    .Select(translation =>
                        new ProductSpecificationTranslationDto
                        {
                            CultureCode = translation.CultureCode,
                            Name = translation.Name,
                            Value = translation.Value
                        })
                    .ToList()
                : new List<ProductSpecificationTranslationDto>()
        };
    }
    private const string DefaultCultureCode = "uk-UA";
    private static readonly HashSet<string> SupportedCultures =
        new(StringComparer.OrdinalIgnoreCase)
        {
        "uk-UA",
        "en-US"
        };
    private static string NormalizeCultureCode(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return DefaultCultureCode;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(
                cultureCode.Trim()).Name;

            return SupportedCultures.Contains(culture)
                ? culture
                : DefaultCultureCode;
        }
        catch (CultureNotFoundException)
        {
            return DefaultCultureCode;
        }
    }
    private static string ValidateTranslationCultureCode(
        string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            throw new ArgumentException(
                "CultureCode is required.");
        }

        string normalized;

        try
        {
            normalized = CultureInfo.GetCultureInfo(
                cultureCode.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            throw new ArgumentException(
                $"Invalid culture code: {cultureCode}.");
        }

        if (!SupportedCultures.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported culture: {normalized}.");
        }

        return normalized;
    }

    private static List<ProductTranslationRequest> NormalizeProductTranslations(
        List<ProductTranslationRequest>? translations)
    {
        if (translations is null || translations.Count == 0)
        {
            throw new ArgumentException(
                "At least one product translation is required.");
        }

        var result = new List<ProductTranslationRequest>();

        var cultures = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var translation in translations)
        {
            if (translation is null)
            {
                throw new ArgumentException(
                    "Product translation cannot be null.");
            }

            var culture = ValidateTranslationCultureCode(
                translation.CultureCode);

            if (!cultures.Add(culture))
            {
                throw new ArgumentException(
                    $"Duplicate product translation culture: {culture}.");
            }

            var name = translation.Name?.Trim();
            var description = translation.Description?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    $"Product name is required for {culture}.");
            }

            if (name.Length > 150 || description.Length > 5000)
            {
                throw new ArgumentException(
                    $"Product translation exceeds allowed length for {culture}.");
            }

            result.Add(new ProductTranslationRequest
            {
                CultureCode = culture,
                Name = name,
                Description = description
            });
        }

        if (!cultures.Contains(DefaultCultureCode))
        {
            throw new ArgumentException(
                "The uk-UA product translation is required.");
        }

        return result;
    }

    private static List<ProductSpecificationTranslationRequest> NormalizeSpecificationTranslations(
            List<ProductSpecificationTranslationRequest>? translations)
    {
        if (translations is null || translations.Count == 0)
        {
            throw new ArgumentException(
                "At least one specification translation is required.");
        }

        var result =
            new List<ProductSpecificationTranslationRequest>();

        var cultures = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var translation in translations)
        {
            if (translation is null)
            {
                throw new ArgumentException(
                    "Specification translation cannot be null.");
            }

            var culture = ValidateTranslationCultureCode(
                translation.CultureCode);

            if (!cultures.Add(culture))
            {
                throw new ArgumentException(
                    $"Duplicate specification translation culture: {culture}.");
            }

            var name = translation.Name?.Trim();
            var value = translation.Value?.Trim();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    $"Specification name and value are required for {culture}.");
            }

            if (name.Length > 150 || value.Length > 1000)
            {
                throw new ArgumentException(
                    $"Specification translation exceeds allowed length for {culture}.");
            }

            result.Add(new ProductSpecificationTranslationRequest
            {
                CultureCode = culture,
                Name = name,
                Value = value
            });
        }

        if (!cultures.Contains(DefaultCultureCode))
        {
            throw new ArgumentException(
                "The uk-UA specification translation is required.");
        }

        return result;
    }
    private static void EnsureUniqueSpecificationNames(
        IEnumerable<ProductSpecificationTranslationRequest> translations)
    {
        var duplicate = translations
            .GroupBy(
                translation => translation.CultureCode,
                StringComparer.OrdinalIgnoreCase)
            .Any(cultureGroup =>
                cultureGroup
                    .GroupBy(
                        translation => translation.Name,
                        StringComparer.OrdinalIgnoreCase)
                    .Any(nameGroup => nameGroup.Count() > 1));

        if (duplicate)
        {
            throw new ArgumentException(
                "Duplicate specification names are not allowed " +
                "within the same culture.");
        }
    }
    private async Task EnsureSpecificationTranslationsAreUniqueAsync(
        Guid productId,
        List<ProductSpecificationTranslationRequest> translations,
        Guid? excludeSpecificationId = null)
    {
        var cultures = translations
            .Select(translation => translation.CultureCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var query = _dataContext.ProductSpecificationTranslations
            .AsNoTracking()
            .Where(translation =>
                translation.ProductId == productId &&
                cultures.Contains(translation.CultureCode));

        if (excludeSpecificationId.HasValue)
        {
            query = query.Where(translation =>
                translation.SpecificationId != excludeSpecificationId.Value);
        }

        var existingTranslations = await query
            .Select(translation => new
            {
                translation.CultureCode,
                translation.Name
            })
            .ToListAsync();

        foreach (var translation in translations)
        {
            var duplicate = existingTranslations.Any(existing =>
                string.Equals(
                    existing.CultureCode,
                    translation.CultureCode,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    existing.Name,
                    translation.Name,
                    StringComparison.OrdinalIgnoreCase));

            if (duplicate)
            {
                throw new InvalidOperationException(
                    $"A specification with this name already exists " +
                    $"for culture {translation.CultureCode}.");
            }
        }
    }
    private async Task SyncProductTranslationsAsync(
        Product product,
        List<ProductTranslationRequest> translations)
    {
        var incoming = translations.ToDictionary(
            translation => translation.CultureCode,
            StringComparer.OrdinalIgnoreCase);

        foreach (var existing in product.Translations.ToList())
        {
            if (!incoming.TryGetValue(
                    existing.CultureCode,
                    out var updated))
            {
                _dataContext.ProductTranslations.Remove(existing);
                product.Translations.Remove(existing);
                continue;
            }

            existing.Name = updated.Name;
            existing.Description = updated.Description;
        }

        var existingCultures = product.Translations
            .Select(translation => translation.CultureCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var translation in translations)
        {
            if (existingCultures.Contains(translation.CultureCode))
            {
                continue;
            }

            var entity = new ProductTranslation
            {
                Id = await GetDbIdentityAsync(),
                ProductId = product.Id,
                CultureCode = translation.CultureCode,
                Name = translation.Name,
                Description = translation.Description
            };

            product.Translations.Add(entity);
            _dataContext.ProductTranslations.Add(entity);
        }

    }
    private async Task SyncSpecificationTranslationsAsync(
        ProductSpecification specification,
        Guid productId,
        List<ProductSpecificationTranslationRequest> translations)
    {
        var incoming = translations.ToDictionary(
            translation => translation.CultureCode,
            StringComparer.OrdinalIgnoreCase);

        foreach (var existing in specification.Translations.ToList())
        {
            if (!incoming.TryGetValue(
                    existing.CultureCode,
                    out var updated))
            {
                _dataContext.ProductSpecificationTranslations
                    .Remove(existing);

                specification.Translations.Remove(existing);
                continue;
            }

            existing.Name = updated.Name;
            existing.Value = updated.Value;
        }

        var existingCultures = specification.Translations
            .Select(translation => translation.CultureCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var translation in translations)
        {
            if (existingCultures.Contains(translation.CultureCode))
            {
                continue;
            }

            var entity = new ProductSpecificationTranslation
            {
                Id = await GetDbIdentityAsync(),
                SpecificationId = specification.Id,
                ProductId = productId,
                CultureCode = translation.CultureCode,
                Name = translation.Name,
                Value = translation.Value
            };

            specification.Translations.Add(entity);

            _dataContext.ProductSpecificationTranslations.Add(entity);
        }
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
                Description = "Допустимые роли: User, Manager или Admin."
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
                Description = "Укажите роль User, Manager или Admin."
            });
        }

        var selectedRole = AppRoles.All.FirstOrDefault(r =>
            string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));

        if (selectedRole is null)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "InvalidRole",
                Description = "Допустимые роли: User, Manager или Admin."
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

        var hasOrders = await _dataContext.Orders
            .AnyAsync(order => order.UserId == id);

        if (hasOrders)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "UserHasOrders",
                Description = "Cannot delete a user who has existing orders."
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

    // CART

    public async Task<CartResponseDto> GetCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        var cartItems = await _dataContext.CartItems
            .AsNoTracking()
            .Where(item => item.Cart.UserId == userId)
            .Include(item => item.Product)
            .ToListAsync(cancellationToken);

        return await BuildCartResponseAsync(cartItems);
    }

    public async Task<CartResponseDto> AddCartItemAsync(
        string userId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }

        // Validate the product before creating a cart.
        var initialProduct = await _dataContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        ValidateCartProduct(initialProduct, quantity);

        // Create a cart only when the user actually adds a product.
        await GetOrCreateCartAsync(userId, cancellationToken);

        await using var transaction =
            await _dataContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Lock the cart row to serialize modifications
        // made by concurrent requests for the same user.
        var cart = await GetCartForUpdateAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            throw new InvalidOperationException(
                "The user's cart could not be retrieved.");
        }

        // Lock and re-read the product to obtain its current stock.
        var product = await GetProductForUpdateAsync(
            productId,
            cancellationToken);

        ValidateCartProduct(product, quantity);

        var existingItem = await _dataContext.CartItems
            .FirstOrDefaultAsync(
                item =>
                    item.CartId == cart.Id &&
                    item.ProductId == productId,
                cancellationToken);

        var currentQuantity = existingItem?.Quantity ?? 0;

        var newQuantity = (long)currentQuantity + quantity;

        if (newQuantity > product!.StockQuantity)
        {
            throw new InvalidOperationException(
                $"Only {product.StockQuantity} units are currently " +
                "available in stock. The requested quantity exceeds " +
                "the available stock.");
        }

        if (existingItem is not null)
        {
            existingItem.Quantity = (int)newQuantity;
        }
        else
        {
            _dataContext.CartItems.Add(new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = productId,
                Quantity = quantity
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetCartAsync(userId, cancellationToken);
    }
    public async Task<CartResponseDto?> UpdateCartItemQuantityAsync(
        string userId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity must be greater than zero.");
        }

        await using var transaction =
            await _dataContext.Database.BeginTransactionAsync(
                cancellationToken);

        var cart = await GetCartForUpdateAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            return null;
        }

        var item = await _dataContext.CartItems
            .FirstOrDefaultAsync(
                existing =>
                    existing.CartId == cart.Id &&
                    existing.ProductId == productId,
                cancellationToken);

        if (item is null)
        {
            return null;
        }

        var product = await GetProductForUpdateAsync(
            productId,
            cancellationToken);

        ValidateCartProduct(product, quantity);

        item.Quantity = quantity;

        cart.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetCartAsync(userId, cancellationToken);
    }
    public async Task<CartResponseDto?> RemoveCartItemAsync(
        string userId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dataContext.Database.BeginTransactionAsync(
                cancellationToken);

        var cart = await GetCartForUpdateAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            return null;
        }

        var item = await _dataContext.CartItems
            .FirstOrDefaultAsync(
                existing =>
                    existing.CartId == cart.Id &&
                    existing.ProductId == productId,
                cancellationToken);

        if (item is null)
        {
            return null;
        }

        _dataContext.CartItems.Remove(item);

        cart.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetCartAsync(userId, cancellationToken);
    }
    public async Task<CartResponseDto> ClearCartAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dataContext.Database.BeginTransactionAsync(
                cancellationToken);

        var cart = await GetCartForUpdateAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            await transaction.CommitAsync(cancellationToken);

            return new CartResponseDto();
        }

        var items = await _dataContext.CartItems
            .Where(item => item.CartId == cart.Id)
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            _dataContext.CartItems.RemoveRange(items);

            cart.UpdatedAt = DateTime.UtcNow;

            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CartResponseDto();
    }


    private async Task<Cart> GetOrCreateCartAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var existingCart = await _dataContext.Carts
            .FirstOrDefaultAsync(
                cart => cart.UserId == userId,
                cancellationToken);

        if (existingCart is not null)
        {
            return existingCart;
        }

        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dataContext.Carts.Add(cart);

        try
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsDuplicateKeyViolation(exception))
        {
            // Another concurrent request may have created
            // the cart for this user first.
            _dataContext.Entry(cart).State = EntityState.Detached;

            var concurrentlyCreatedCart =
                await _dataContext.Carts.FirstOrDefaultAsync(
                    existing => existing.UserId == userId,
                    cancellationToken);

            if (concurrentlyCreatedCart is null)
            {
                throw new InvalidOperationException(
                    "The user's cart could not be created.",
                    exception);
            }

            return concurrentlyCreatedCart;
        }

        return cart;
    }
    private async Task<Product?> GetProductForUpdateAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var products = await _dataContext.Products
            .FromSqlInterpolated(
                $"SELECT * FROM `Products` WHERE `Id` = {productId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return products.SingleOrDefault();
    }
    private async Task<Cart?> GetCartForUpdateAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var carts = await _dataContext.Carts
            .FromSqlInterpolated(
                $"SELECT * FROM `Carts` WHERE `UserId` = {userId} FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);

        return carts.SingleOrDefault();
    }
    private static void ValidateCartProduct(
        Product? product,
        int quantity)
    {
        if (product is null)
        {
            throw new KeyNotFoundException(
                "Product not found.");
        }

        if (!product.IsActive)
        {
            throw new InvalidOperationException(
                "The product is inactive and cannot be purchased.");
        }

        if (product.StockQuantity <= 0)
        {
            throw new InvalidOperationException(
                "The product is out of stock.");
        }

        if (quantity > product.StockQuantity)
        {
            throw new InvalidOperationException(
                $"Only {product.StockQuantity} units are currently available.");
        }
    }
    private async Task<CartResponseDto> BuildCartResponseAsync(
        List<CartItem> cartItems)
    {
        var productIds = cartItems
            .Select(item => item.ProductId)
            .Distinct()
            .ToList();

        var promotionDiscounts = await GetActivePromotionDiscountsAsync(
            productIds);

        var responseItems = new List<CartItemDto>();

        foreach (var item in cartItems)
        {
            var product = item.Product;

            var promotion = CalculateBestPromotionPrice(
                product.Price,
                promotionDiscounts.GetValueOrDefault(product.Id));

            var stockQuantity = Math.Max(
                product.StockQuantity,
                0);

            var availableQuantity = product.IsActive
                ? Math.Min(item.Quantity, stockQuantity)
                : 0;

            var availabilityStatus = !product.IsActive
                ? "Inactive"
                : stockQuantity == 0
                    ? "OutOfStock"
                    : item.Quantity > stockQuantity
                        ? "InsufficientStock"
                        : "Available";

            var unitPrice = promotion?.DiscountedPrice
                ?? product.Price;

            var lineTotal = decimal.Round(
                unitPrice * availableQuantity,
                2,
                MidpointRounding.AwayFromZero);

            responseItems.Add(new CartItemDto
            {
                Id = item.Id,
                ProductId = product.Id,
                Sku = product.Sku,

                OriginalUnitPrice = product.Price,
                UnitPrice = unitPrice,

                ActivePromotion = promotion,

                RequestedQuantity = item.Quantity,
                AvailableQuantity = availableQuantity,
                StockQuantity = stockQuantity,

                IsProductActive = product.IsActive,
                IsAvailable = availableQuantity > 0,

                IsQuantityAdjusted =
                    item.Quantity != availableQuantity,

                AvailabilityStatus = availabilityStatus,

                LineTotal = lineTotal
            });
        }

        var totalAmount = decimal.Round(
            responseItems.Sum(item => item.LineTotal),
            2,
            MidpointRounding.AwayFromZero);

        return new CartResponseDto
        {
            Items = responseItems,

            TotalAvailableQuantity = responseItems.Sum(
                item => item.AvailableQuantity),

            TotalAmount = totalAmount
        };
    }

    // Order

    public async Task<IReadOnlyList<OrderDto>> GetMyOrdersAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        var orders = await _dataContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .ToListAsync(cancellationToken);

        return orders
            .Select(MapOrderToDto)
            .ToList();
    }
    public async Task<IReadOnlyList<OrderDto>> GetAllOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var orders = await _dataContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .ToListAsync(cancellationToken);

        return orders
            .Select(MapOrderToDto)
            .ToList();
    }
    public async Task<OrderDto> CreateOrderAsync(
        string userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "User ID is required.",
                nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.City) ||
            string.IsNullOrWhiteSpace(request.DeliveryAddress))
        {
            throw new ArgumentException(
                "All required delivery fields must be provided.");
        }

        await using var transaction =
            await _dataContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Lock the user's cart to prevent concurrent modifications.
        var cart = await GetCartForUpdateAsync(
            userId,
            cancellationToken);

        if (cart is null)
        {
            throw new InvalidOperationException(
                "Your cart is empty.");
        }

        var cartItems = await _dataContext.CartItems
            .Where(item => item.CartId == cart.Id)
            .OrderBy(item => item.ProductId)
            .ToListAsync(cancellationToken);

        if (cartItems.Count == 0)
        {
            throw new InvalidOperationException(
                "Your cart is empty.");
        }

        // Lock all products in a consistent order.
        // This reduces the risk of deadlocks between concurrent checkouts.
        var products = new Dictionary<Guid, Product>();

        foreach (var productId in cartItems
            .Select(item => item.ProductId)
            .Distinct()
            .OrderBy(id => id))
        {
            var product = await GetProductForOrderUpdateAsync(
                productId,
                cancellationToken);

            if (product is null)
            {
                throw new InvalidOperationException(
                    "One or more products in your cart no longer exist.");
            }

            products.Add(productId, product);
        }

        // Revalidate products and stock immediately before purchase.
        foreach (var item in cartItems)
        {
            var product = products[item.ProductId];


            if (product.StockQuantity < item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Requested: {item.Quantity}. " +
                    $"Available: {product.StockQuantity}. " +
                    "Please update your cart.");
            }
        }

        // Retrieve current promotions using the existing discount logic.
        var promotionDiscounts = await GetActivePromotionDiscountsAsync(
            products.Keys);

        var now = DateTime.UtcNow;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CustomerName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            City = request.City.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            Comment = string.IsNullOrWhiteSpace(request.Comment)
                ? null
                : request.Comment.Trim(),
            Status = OrderStatus.New,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var cartItem in cartItems)
        {
            var product = products[cartItem.ProductId];

            var promotion = CalculateBestPromotionPrice(
                product.Price,
                promotionDiscounts.GetValueOrDefault(product.Id));

            // Store the final price, including the active discount.
            var unitPrice = promotion?.DiscountedPrice
                ?? product.Price;

            var lineTotal = decimal.Round(
                unitPrice * cartItem.Quantity,
                2,
                MidpointRounding.AwayFromZero);

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                ProductSku = product.Sku,
                UnitPrice = unitPrice,
                Quantity = cartItem.Quantity,
                TotalPrice = lineTotal
            });

            // Reserve the purchased quantity by decreasing stock.
            product.StockQuantity -= cartItem.Quantity;
            product.UpdatedAt = now;
        }

        order.TotalAmount = decimal.Round(
            order.Items.Sum(item => item.TotalPrice),
            2,
            MidpointRounding.AwayFromZero);

        _dataContext.Orders.Add(order);

        // Clear the cart only as part of the same transaction.
        _dataContext.CartItems.RemoveRange(cartItems);

        cart.UpdatedAt = now;

        await _dataContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return MapOrderToDto(order);
    }
    public async Task<OrderDto?> UpdateOrderStatusAsync(
        Guid orderId,
        OrderStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Order ID cannot be empty.",
                nameof(orderId));
        }

        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(newStatus),
                "Invalid order status.");
        }

        await using var transaction =
            await _dataContext.Database.BeginTransactionAsync(
                cancellationToken);

        // Lock the order to prevent concurrent status changes.
        var order = await GetOrderForUpdateAsync(
            orderId,
            cancellationToken);

        if (order is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        // Repeating the same status must not repeat side effects.
        if (order.Status == newStatus)
        {
            await transaction.CommitAsync(cancellationToken);

            return await GetOrderByIdAsync(
                orderId,
                cancellationToken);
        }

        if (!CanChangeOrderStatus(order.Status, newStatus))
        {
            throw new InvalidOperationException(
                $"Order status cannot be changed from " +
                $"'{order.Status}' to '{newStatus}'.");
        }

        if (newStatus == OrderStatus.Cancelled)
        {
            var orderItems = await _dataContext.OrderItems
                .Where(item => item.OrderId == orderId)
                .OrderBy(item => item.ProductId)
                .ToListAsync(cancellationToken);

            // Lock all products in a consistent order.
            var products = new Dictionary<Guid, Product>();

            foreach (var productId in orderItems
                .Select(item => item.ProductId)
                .Distinct()
                .OrderBy(id => id))
            {
                var product = await GetProductForOrderUpdateAsync(
                    productId,
                    cancellationToken);

                if (product is null)
                {
                    throw new InvalidOperationException(
                        $"Product '{productId}' referenced by the order " +
                        "could not be found.");
                }

                products.Add(productId, product);
            }

            var now = DateTime.UtcNow;

            // Restore the quantities reserved by this order.
            foreach (var item in orderItems)
            {
                var product = products[item.ProductId];

                var restoredStock =
                    (long)product.StockQuantity + item.Quantity;

                if (restoredStock > int.MaxValue)
                {
                    throw new InvalidOperationException(
                       "would exceed the maximum supported quantity.");
                }

                product.StockQuantity = (int)restoredStock;
                product.UpdatedAt = now;
            }
        }

        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await _dataContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return await GetOrderByIdAsync(
            orderId,
            cancellationToken);
    }



    public async Task<OrderDto?> GetOrderByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dataContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.Id == orderId,
                cancellationToken);

        return order is null
            ? null
            : MapOrderToDto(order);
    }
    private async Task<Order?> GetOrderForUpdateAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var orders = await _dataContext.Orders
            .FromSqlInterpolated(
                $"SELECT * FROM `Orders` WHERE `Id` = {orderId} FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);

        return orders.SingleOrDefault();
    }
    private static bool CanChangeOrderStatus(
        OrderStatus currentStatus,
        OrderStatus newStatus)
    {
        return currentStatus switch
        {
            OrderStatus.New =>
                newStatus is OrderStatus.Processing
                    or OrderStatus.Cancelled,

            OrderStatus.Processing =>
                newStatus is OrderStatus.Completed
                    or OrderStatus.Cancelled,

            OrderStatus.Completed => false,

            OrderStatus.Cancelled => false,

            _ => false
        };
    }
    private async Task<Product?> GetProductForOrderUpdateAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var products = await _dataContext.Products
            .FromSqlInterpolated(
                $"SELECT * FROM `Products` WHERE `Id` = {productId} FOR UPDATE")
            .AsTracking()
            .ToListAsync(cancellationToken);

        return products.SingleOrDefault();
    }
    private static OrderDto MapOrderToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            CustomerName = order.CustomerName,
            PhoneNumber = order.PhoneNumber,
            City = order.City,
            DeliveryAddress = order.DeliveryAddress,
            Comment = order.Comment,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,

            Items = order.Items
                .Select(item => new OrderItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    ProductSku = item.ProductSku,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    TotalPrice = item.TotalPrice
                })
                .ToList()
        };
    }








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

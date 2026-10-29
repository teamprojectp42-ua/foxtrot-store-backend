using foxtrot_store_backend.Services.LocalStorage;
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(
    DataAccessor dataAccessor,
    ILocalStorageService localStorage) : ControllerBase
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

        if (query.Page >= 1 &&
            query.PageSize >= 1 &&
            query.PageSize <= 100)
        {
            var requestedOffset =
                ((long)query.Page - 1) * query.PageSize;

            if (requestedOffset > int.MaxValue)
            {
                ModelState.AddModelError(
                    nameof(query.Page),
                    "The requested page is too large.");
            }
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
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "Products",
                    DataType = "application/json",
                    Manipulations = ["GET"],
                    Links = { ["self"] = "/api/products" }
                },
                Data = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors
                            .Select(error => error.ErrorMessage)
                            .ToArray())
            });
        }

        var result = await _dataAccessor.GetProductsAsync(query);

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Products",
                DataType = "json/array",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links = { ["self"] = "/api/products" },
                Pagination = new RestMetaPagination
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalPages = result.TotalPages,
                    TotalItems = result.TotalCount
                },
                Parameters = new Dictionary<string, object>
                {
                    ["sortBy"] = query.SortBy,
                    ["sortDirection"] = query.SortDirection
                }
            },
            Data = result.Items
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var product = await _dataAccessor.GetProductByIdAsync(id);

        if (product is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    Links = { ["self"] = $"/api/products/{id}" }
                },
                Data = new { message = "Product not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Product",
                DataType = "json/object",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
                {
                    ["parent"] = "/api/products",
                    ["self"] = $"/api/products/{id}"
                }
            },
            Data = product
        });
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetProductBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 150)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    Links = { ["self"] = $"/api/products/slug/{slug}" }
                },
                Data = new
                {
                    message = "Slug must contain between 1 and 150 characters."
                }
            });
        }

        var product = await _dataAccessor.GetProductBySlugAsync(slug);

        if (product is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    Links = { ["self"] = $"/api/products/slug/{slug}" }
                },
                Data = new { message = "Product not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Product",
                DataType = "json/object",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
                {
                    ["parent"] = "/api/products",
                    ["self"] = $"/api/products/slug/{slug}"
                }
            },
            Data = product
        });
    }

    [HttpGet("images/{imageKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductImage(
    string imageKey,
    CancellationToken cancellationToken)
    {
        var contentType = Path.GetExtension(imageKey).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        if (contentType is null)
        {
            return BadRequest(new
            {
                message = "Unsupported image format."
            });
        }

        Stream? imageStream;

        try
        {
            imageStream = await localStorage.GetAsync(
                imageKey,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            return BadRequest(new
            {
                message = "Invalid image key."
            });
        }

        if (imageStream is null)
        {
            return NotFound(new
            {
                message = "Image not found."
            });
        }

        return File(imageStream, contentType);
    }

    [HttpGet("{productId:guid}/specifications")]
    public async Task<IActionResult> GetProductSpecifications(
        Guid productId)
    {
        var path = $"/api/products/{productId}/specifications";

        var specifications =
            await _dataAccessor.GetProductSpecificationsAsync(
                productId,
                User.IsInRole("Admin"));

        if (specifications is null)
        {
            return NotFound(CreateSpecificationResponse(
                RestStatus.NotFound,
                new { message = "Product not found." },
                "json/object",
                path,
                "GET"));
        }

        return Ok(CreateSpecificationResponse(
            RestStatus.Ok,
            specifications,
            "json/array",
            path,
            "GET"));
    }

    [HttpGet(
    "{productId:guid}/specifications/{specificationId:guid}")]
    public async Task<IActionResult> GetProductSpecification(
        Guid productId,
        Guid specificationId)
    {
        var path =
            $"/api/products/{productId}/specifications/{specificationId}";

        var specification =
            await _dataAccessor.GetProductSpecificationAsync(
                productId,
                specificationId,
                User.IsInRole("Admin"));

        if (specification is null)
        {
            return NotFound(CreateSpecificationResponse(
                RestStatus.NotFound,
                new { message = "Product or specification not found." },
                "json/object",
                path,
                "GET"));
        }

        return Ok(CreateSpecificationResponse(
            RestStatus.Ok,
            specification,
            "json/object",
            path,
            "GET"));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        [FromBody] ProductCreateRequest request)
    {
        try
        {
            var createdProduct =
                await _dataAccessor.CreateProductAsync(request);

            var response = new RestResponse
            {
                Status = RestStatus.Created,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    DataType = "json/object",
                    CacheTime = 0,
                    Manipulations = ["GET"],
                    Links =
                    {
                        ["parent"] = "/api/products",
                        ["self"] =
                            $"/api/products/{createdProduct.Id}"
                    }
                },
                Data = createdProduct
            };

            return StatusCode(
                StatusCodes.Status201Created,
                response);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    Manipulations = ["POST"],
                    Links = { ["self"] = "/api/products" }
                },
                Data = new { message = exception.Message }
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    Manipulations = ["POST"],
                    Links = { ["self"] = "/api/products" }
                },
                Data = new { message = exception.Message }
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{productId:guid}/specifications")]
    public async Task<IActionResult> CreateProductSpecification(
        Guid productId,
        [FromBody] ProductSpecificationCreateRequest request)
    {
        var path = $"/api/products/{productId}/specifications";

        try
        {
            var specification =
                await _dataAccessor.CreateProductSpecificationAsync(
                    productId,
                    request);

            var response = CreateSpecificationResponse(
                RestStatus.Created,
                specification,
                "json/object",
                $"{path}/{specification.Id}",
                "GET");

            return CreatedAtAction(
                nameof(GetProductSpecification),
                new
                {
                    productId,
                    specificationId = specification.Id
                },
                response);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(CreateSpecificationResponse(
                RestStatus.NotFound,
                new { message = exception.Message },
                "json/object",
                path,
                "POST"));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateSpecificationResponse(
                RestStatus.Conflict,
                new { message = exception.Message },
                "json/object",
                path,
                "POST"));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateSpecificationResponse(
                RestStatus.BadRequest,
                new { message = exception.Message },
                "json/object",
                path,
                "POST"));
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(
        Guid id,
        [FromBody] ProductUpdateRequest request)
    {
        try
        {
            var product = await _dataAccessor.UpdateProductAsync(id, request);

            if (product is null)
            {
                return NotFound(new RestResponse
                {
                    Status = RestStatus.NotFound,
                    Meta = new RestMeta { ApiName = "Product" },
                    Data = new { message = "Product not found." }
                });
            }

            return Ok(new RestResponse
            {
                Status = RestStatus.Ok,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    DataType = "json/object",
                    Manipulations = ["GET", "PUT", "DELETE"],
                    Links = { ["self"] = $"/api/products/{id}" }
                },
                Data = product
            });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta { ApiName = "Product" },
                Data = new { message = exception.Message }
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta { ApiName = "Product" },
                Data = new { message = exception.Message }
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut(
    "{productId:guid}/specifications/{specificationId:guid}")]
    public async Task<IActionResult> UpdateProductSpecification(
        Guid productId,
        Guid specificationId,
        [FromBody] ProductSpecificationUpdateRequest request)
    {
        var path =
            $"/api/products/{productId}/specifications/{specificationId}";

        try
        {
            var specification =
                await _dataAccessor.UpdateProductSpecificationAsync(
                    productId,
                    specificationId,
                    request);

            if (specification is null)
            {
                return NotFound(CreateSpecificationResponse(
                    RestStatus.NotFound,
                    new { message = "Product or specification not found." },
                    "json/object",
                    path,
                    "PUT"));
            }

            return Ok(CreateSpecificationResponse(
                RestStatus.Ok,
                specification,
                "json/object",
                path,
                "PUT"));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(CreateSpecificationResponse(
                RestStatus.Conflict,
                new { message = exception.Message },
                "json/object",
                path,
                "PUT"));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(CreateSpecificationResponse(
                RestStatus.BadRequest,
                new { message = exception.Message },
                "json/object",
                path,
                "PUT"));
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var deleted = await _dataAccessor.DeleteProductAsync(id);

        if (!deleted)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta { ApiName = "Product" },
                Data = new { message = "Product not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta { ApiName = "Product" },
            Data = new { message = "Product deactivated successfully." }
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete(
    "{productId:guid}/specifications/{specificationId:guid}")]
    public async Task<IActionResult> DeleteProductSpecification(
        Guid productId,
        Guid specificationId)
    {
        var path =
            $"/api/products/{productId}/specifications/{specificationId}";

        var deleted =
            await _dataAccessor.DeleteProductSpecificationAsync(
                productId,
                specificationId);

        if (!deleted)
        {
            return NotFound(CreateSpecificationResponse(
                RestStatus.NotFound,
                new { message = "Product or specification not found." },
                "json/object",
                path,
                "DELETE"));
        }

        return Ok(CreateSpecificationResponse(
            RestStatus.Ok,
            new { message = "Product specification deleted successfully." },
            "json/object",
            path,
            "DELETE"));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreProduct(Guid id)
    {
        var restored = await _dataAccessor.RestoreProductAsync(id);

        if (!restored)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Product",
                    Links =
                {
                    ["self"] = $"/api/products/{id}/restore"
                }
                },
                Data = new
                {
                    message = "Inactive product not found or already active."
                }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Product",
                DataType = "json/object",
                Manipulations = ["POST"],
                Links =
            {
                ["self"] = $"/api/products/{id}/restore",
                ["parent"] = "/api/products"
            }
            },
            Data = new
            {
                message = "Product restored successfully."
            }
        });
    }

    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProductImage(
    Guid id,
    [FromForm] IFormFile? file,
    [FromForm] bool isPrimary = false,
    [FromForm] int sortOrder = 0,
    CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("An image file is required.");
        }

        if (sortOrder < 0)
        {
            return BadRequest("SortOrder cannot be negative.");
        }

        string storageKey;

        try
        {
            await using var stream = file.OpenReadStream();

            storageKey = await localStorage.SaveAsync(
                stream,
                file.ContentType,
                cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { message = exception.Message });
        }

        try
        {
            var added = await dataAccessor.AddProductImageAsync(
                id,
                storageKey,
                isPrimary,
                sortOrder,
                cancellationToken);

            if (!added)
            {
                await localStorage.DeleteAsync(
                    storageKey,
                    CancellationToken.None);

                return NotFound(new
                {
                    message = "Active product was not found."
                });
            }
        }
        catch
        {
            // If database persistence fails, remove the newly stored file.
            try
            {
                await localStorage.DeleteAsync(
                    storageKey,
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                // Cleanup failure must not hide the original database error.
                HttpContext.RequestServices
                    .GetRequiredService<ILogger<ProductsController>>()
                    .LogError(
                        cleanupException,
                        "Failed to remove image {StorageKey} after upload failure.",
                        storageKey);
            }

            throw;
        }

        return Ok(new
        {
            productId = id,
            imageKey = storageKey,
            isPrimary,
            sortOrder
        });
    }


    private static RestResponse CreateSpecificationResponse(
        RestStatus status,
        object? data,
        string dataType,
        string path,
        string method)
    {
        return new RestResponse
        {
            Status = status,
            Meta = new RestMeta
            {
                ApiName = "ProductSpecifications",
                DataType = dataType,
                CacheTime = 0,
                Manipulations = [method],
                Links =
            {
                ["parent"] = "/api/products",
                ["self"] = path
            }
            },
            Data = data
        };
    }
}

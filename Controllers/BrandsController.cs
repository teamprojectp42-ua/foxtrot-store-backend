using Microsoft.AspNetCore.Authorization;
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Brands;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Mvc;
using foxtrot_store_backend.Services.LocalStorage;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/brands")]
public class BrandsController(
    DataAccessor dataAccessor,
    ILocalStorageService localStorage,
    ILogger<BrandsController> logger) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;
    private readonly ILocalStorageService _localStorage = localStorage;
    private readonly ILogger<BrandsController> _logger = logger;

    [HttpGet]
    public async Task<IActionResult> GetBrands()
    {
        var brands = await _dataAccessor.GetBrandsAsync();

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Brands",
                DataType = "json/array",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links = { ["self"] = "/api/brands" }
            },
            Data = brands
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBrandById(Guid id)
    {
        var brand = await _dataAccessor.GetBrandByIdAsync(id);

        if (brand is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Brand",
                    Links = { ["self"] = $"/api/brands/{id}" }
                },
                Data = new { message = "Brand not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Brand",
                DataType = "json/object",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
                {
                    ["parent"] = "/api/brands",
                    ["self"] = $"/api/brands/{id}"
                }
            },
            Data = brand
        });
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBrandBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 150)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "Brand",
                    Links = { ["self"] = $"/api/brands/slug/{slug}" }
                },
                Data = new
                {
                    message = "Slug must contain between 1 and 150 characters."
                }
            });
        }

        var brand = await _dataAccessor.GetBrandBySlugAsync(slug);

        if (brand is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Brand",
                    Links = { ["self"] = $"/api/brands/slug/{slug}" }
                },
                Data = new { message = "Brand not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Brand",
                DataType = "json/object",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
                {
                    ["parent"] = "/api/brands",
                    ["self"] = $"/api/brands/slug/{slug}"
                }
            },
            Data = brand
        });
    }

    [HttpGet("{id:guid}/products")]
    public async Task<IActionResult> GetBrandProducts(
    Guid id,
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

        var allowedSortFields = new[]
        {
        "name",
        "price",
        "createdat"
    };

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
                    ApiName = "BrandProducts",
                    DataType = "application/json",
                    Manipulations = ["GET"],
                    Links =
                {
                    ["self"] = $"/api/brands/{id}/products"
                }
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

        if (!await _dataAccessor.BrandExistsAsync(id))
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "BrandProducts",
                    Links =
                {
                    ["self"] = $"/api/brands/{id}/products"
                }
                },
                Data = new
                {
                    message = "Brand not found."
                }
            });
        }

        // Always use the brand ID supplied in the route.
        query.BrandId = id;

        var result = await _dataAccessor.GetProductsAsync(query);

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "BrandProducts",
                DataType = "json/array",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
            {
                ["self"] = $"/api/brands/{id}/products"
            },
                Pagination = new RestMetaPagination
                {
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalPages = result.TotalPages,
                    TotalItems = result.TotalCount
                },
                Parameters = new Dictionary<string, object>
                {
                    ["brandId"] = id,
                    ["sortBy"] = query.SortBy,
                    ["sortDirection"] = query.SortDirection
                }
            },
            Data = result.Items
        });
    }

    [HttpGet("images/{imageKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBrandLogo(
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
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "BrandLogo"
                },
                Data = new
                {
                    message = "Unsupported image format."
                }
            });
        }

        Stream? imageStream;

        try
        {
            imageStream = await _localStorage.GetAsync(
                imageKey,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "BrandLogo"
                },
                Data = new
                {
                    message = "Invalid image key."
                }
            });
        }

        if (imageStream is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "BrandLogo"
                },
                Data = new
                {
                    message = "Image not found."
                }
            });
        }

        return File(imageStream, contentType);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadBrandLogo(
    Guid id,
    [FromForm] IFormFile? file,
    CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "BrandLogo"
                },
                Data = new
                {
                    message = "A logo image file is required."
                }
            });
        }

        string logoKey;

        try
        {
            await using var stream = file.OpenReadStream();

            logoKey = await _localStorage.SaveAsync(
                stream,
                file.ContentType,
                cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "BrandLogo"
                },
                Data = new
                {
                    message = exception.Message
                }
            });
        }

        (bool Updated, string? PreviousLogoKey) result;

        try
        {
            result = await _dataAccessor.SetBrandLogoAsync(
                id,
                logoKey,
                cancellationToken);

            if (!result.Updated)
            {
                await TryDeleteLogoAsync(logoKey);

                return NotFound(new RestResponse
                {
                    Status = RestStatus.NotFound,
                    Meta = new RestMeta
                    {
                        ApiName = "BrandLogo",
                        Links =
                    {
                        ["self"] = $"/api/brands/{id}/logo"
                    }
                    },
                    Data = new
                    {
                        message = "Active brand was not found."
                    }
                });
            }
        }
        catch
        {
            // Remove the uploaded file if database persistence fails.
            await TryDeleteLogoAsync(logoKey);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(result.PreviousLogoKey) &&
            !string.Equals(
                result.PreviousLogoKey,
                logoKey,
                StringComparison.Ordinal) &&
            IsLocalStorageLogoKey(result.PreviousLogoKey))
        {
            // The database already references the new logo.
            // Failure to remove the old file must not invalidate the update.
            await TryDeleteLogoAsync(result.PreviousLogoKey);
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "BrandLogo",
                DataType = "application/json",
                Manipulations = ["POST"],
                Links =
            {
                ["parent"] = "/api/brands",
                ["logo"] = $"/api/brands/images/{logoKey}"
            }
            },
            Data = new
            {
                brandId = id,
                logoKey,
                logoUrl = $"/api/brands/images/{logoKey}"
            }
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateBrand(
        [FromBody] BrandCreateRequest request)
    {
        try
        {
            var brand = await _dataAccessor.CreateBrandAsync(request);

            return StatusCode(StatusCodes.Status201Created,
                new RestResponse
                {
                    Status = RestStatus.Created,
                    Meta = new RestMeta
                    {
                        ApiName = "Brand",
                        DataType = "json/object",
                        Manipulations = ["GET"],
                        Links =
                        {
                            ["parent"] = "/api/brands",
                            ["self"] = $"/api/brands/{brand.Id}"
                        }
                    },
                    Data = brand
                });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta { ApiName = "Brand" },
                Data = new { message = ex.Message }
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBrand(
        Guid id,
        [FromBody] BrandUpdateRequest request)
    {
        try
        {
            var brand = await _dataAccessor.UpdateBrandAsync(id, request);

            if (brand is null)
            {
                return NotFound(new RestResponse
                {
                    Status = RestStatus.NotFound,
                    Meta = new RestMeta { ApiName = "Brand" },
                    Data = new { message = "Brand not found." }
                });
            }

            return Ok(new RestResponse
            {
                Status = RestStatus.Ok,
                Meta = new RestMeta { ApiName = "Brand" },
                Data = brand
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta { ApiName = "Brand" },
                Data = new { message = ex.Message }
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBrand(Guid id)
    {
        var deleted = await _dataAccessor.DeleteBrandAsync(id);

        if (!deleted)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Brand",
                    Links =
                {
                    ["self"] = $"/api/brands/{id}"
                }
                },
                Data = new
                {
                    message = "Active brand not found."
                }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Brand",
                Manipulations = ["DELETE"],
                Links =
            {
                ["self"] = $"/api/brands/{id}",
                ["restore"] = $"/api/brands/{id}/restore"
            }
            },
            Data = new
            {
                message = "Brand deactivated successfully."
            }
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreBrand(Guid id)
    {
        var restored = await _dataAccessor.RestoreBrandAsync(id);

        if (!restored)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Brand",
                    Links =
                {
                    ["self"] = $"/api/brands/{id}/restore"
                }
                },
                Data = new
                {
                    message = "Inactive brand not found or already active."
                }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Brand",
                DataType = "application/json",
                Manipulations = ["POST"],
                Links =
            {
                ["self"] = $"/api/brands/{id}/restore",
                ["parent"] = "/api/brands"
            }
            },
            Data = new
            {
                message = "Brand restored successfully."
            }
        });
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

    private async Task TryDeleteLogoAsync(string logoKey)
    {
        try
        {
            await _localStorage.DeleteAsync(
                logoKey,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Cleanup failure must not hide the original result or exception.
            _logger.LogWarning(
                exception,
                "Failed to remove brand logo {LogoKey}.",
                logoKey);
        }
    }
}

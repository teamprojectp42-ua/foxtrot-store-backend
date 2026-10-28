using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Categories;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using foxtrot_store_backend.Services.LocalStorage;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(
    DataAccessor dataAccessor,
    ILocalStorageService localStorage,
    ILogger<CategoriesController> logger) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;
    private readonly ILocalStorageService _localStorage = localStorage;
    private readonly ILogger<CategoriesController> _logger = logger;


    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _dataAccessor.GetCategoriesAsync();

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Categories",
                DataType = "json/array",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links = { ["self"] = "/api/categories" }
            },
            Data = categories
        });
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetCategoryTree()
    {
        var categories = await _dataAccessor.GetCategoryTreeAsync();

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "CategoryTree",
                DataType = "json/array",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links = { ["self"] = "/api/categories/tree" }
            },
            Data = categories
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCategoryById(Guid id)
    {
        var category = await _dataAccessor.GetCategoryByIdAsync(id);

        if (category is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Category",
                    Links = { ["self"] = $"/api/categories/{id}" }
                },
                Data = new { message = "Category not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Category",
                DataType = "json/object",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
                {
                    ["parent"] = "/api/categories",
                    ["self"] = $"/api/categories/{id}"
                }
            },
            Data = category
        });
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetCategoryBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 150)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "Category",
                    Links = { ["self"] = $"/api/categories/slug/{slug}" }
                },
                Data = new
                {
                    message = "Slug must contain between 1 and 150 characters."
                }
            });
        }

        var category = await _dataAccessor.GetCategoryBySlugAsync(slug);

        if (category is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Category",
                    Links = { ["self"] = $"/api/categories/slug/{slug}" }
                },
                Data = new { message = "Category not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Category",
                DataType = "json/object",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
                {
                    ["parent"] = "/api/categories",
                    ["self"] = $"/api/categories/slug/{slug}"
                }
            },
            Data = category
        });
    }

    [HttpGet("{id:guid}/products")]
    public async Task<IActionResult> GetCategoryProducts(
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
                    ApiName = "CategoryProducts",
                    DataType = "application/json",
                    Manipulations = ["GET"],
                    Links =
                {
                    ["self"] = $"/api/categories/{id}/products"
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

        if (!await _dataAccessor.CategoryExistsAsync(id))
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "CategoryProducts",
                    Links =
                {
                    ["self"] = $"/api/categories/{id}/products"
                }
                },
                Data = new
                {
                    message = "Category not found."
                }
            });
        }

        // Always use the category ID from the route.
        query.CategoryId = id;

        var result = await _dataAccessor.GetProductsAsync(query);

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "CategoryProducts",
                DataType = "json/array",
                CacheTime = 0,
                Manipulations = ["GET"],
                Links =
            {
                ["self"] = $"/api/categories/{id}/products"
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
                    ["categoryId"] = id,
                    ["sortBy"] = query.SortBy,
                    ["sortDirection"] = query.SortDirection
                }
            },
            Data = result.Items
        });
    }


    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CategoryCreateRequest request)
    {
        try
        {
            var category =
                await _dataAccessor.CreateCategoryAsync(request);

            return StatusCode(StatusCodes.Status201Created,
                new RestResponse
                {
                    Status = RestStatus.Created,
                    Meta = new RestMeta
                    {
                        ApiName = "Category",
                        DataType = "json/object",
                        Manipulations = ["GET"],
                        Links =
                        {
                            ["parent"] = "/api/categories",
                            ["self"] = $"/api/categories/{category.Id}"
                        }
                    },
                    Data = category
                });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta { ApiName = "Category" },
                Data = new { message = ex.Message }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta { ApiName = "Category" },
                Data = new { message = ex.Message }
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] CategoryUpdateRequest request)
    {
        try
        {
            var category =
                await _dataAccessor.UpdateCategoryAsync(id, request);

            if (category is null)
            {
                return NotFound(new RestResponse
                {
                    Status = RestStatus.NotFound,
                    Meta = new RestMeta { ApiName = "Category" },
                    Data = new { message = "Category not found." }
                });
            }

            return Ok(new RestResponse
            {
                Status = RestStatus.Ok,
                Meta = new RestMeta { ApiName = "Category" },
                Data = category
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta { ApiName = "Category" },
                Data = new { message = ex.Message }
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta { ApiName = "Category" },
                Data = new { message = ex.Message }
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        try
        {
            var deleted = await _dataAccessor.DeleteCategoryAsync(id);

            if (!deleted)
            {
                return NotFound(new RestResponse
                {
                    Status = RestStatus.NotFound,
                    Meta = new RestMeta
                    {
                        ApiName = "Category",
                        Links =
                    {
                        ["self"] = $"/api/categories/{id}"
                    }
                    },
                    Data = new
                    {
                        message = "Active category not found."
                    }
                });
            }

            return Ok(new RestResponse
            {
                Status = RestStatus.Ok,
                Meta = new RestMeta
                {
                    ApiName = "Category",
                    Manipulations = ["DELETE"],
                    Links =
                {
                    ["self"] = $"/api/categories/{id}",
                    ["restore"] = $"/api/categories/{id}/restore"
                }
                },
                Data = new
                {
                    message = "Category deactivated successfully."
                }
            });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta
                {
                    ApiName = "Category",
                    Links =
                {
                    ["self"] = $"/api/categories/{id}"
                }
                },
                Data = new
                {
                    message = exception.Message
                }
            });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreCategory(Guid id)
    {
        try
        {
            var restored = await _dataAccessor.RestoreCategoryAsync(id);

            if (!restored)
            {
                return NotFound(new RestResponse
                {
                    Status = RestStatus.NotFound,
                    Meta = new RestMeta
                    {
                        ApiName = "Category",
                        Links =
                    {
                        ["self"] = $"/api/categories/{id}/restore"
                    }
                    },
                    Data = new
                    {
                        message =
                            "Inactive category not found or already active."
                    }
                });
            }

            return Ok(new RestResponse
            {
                Status = RestStatus.Ok,
                Meta = new RestMeta
                {
                    ApiName = "Category",
                    Manipulations = ["POST"],
                    Links =
                {
                    ["self"] = $"/api/categories/{id}/restore",
                    ["parent"] = "/api/categories"
                }
                },
                Data = new
                {
                    message = "Category restored successfully."
                }
            });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new RestResponse
            {
                Status = RestStatus.Conflict,
                Meta = new RestMeta
                {
                    ApiName = "Category",
                    Links =
                {
                    ["self"] = $"/api/categories/{id}/restore"
                }
                },
                Data = new
                {
                    message = exception.Message
                }
            });
        }
    }

    [HttpGet("images/{imageKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategoryImage(
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
                    ApiName = "CategoryImage"
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
                    ApiName = "CategoryImage"
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
                    ApiName = "CategoryImage"
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
    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCategoryImage(
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
                    ApiName = "CategoryImage"
                },
                Data = new
                {
                    message = "An image file is required."
                }
            });
        }

        string imageKey;

        try
        {
            await using var stream = file.OpenReadStream();

            imageKey = await _localStorage.SaveAsync(
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
                    ApiName = "CategoryImage"
                },
                Data = new
                {
                    message = exception.Message
                }
            });
        }

        (bool Updated, string? PreviousImageKey) result;

        try
        {
            result = await _dataAccessor.SetCategoryImageAsync(
                id,
                imageKey,
                cancellationToken);

            if (!result.Updated)
            {
                await TryDeleteImageAsync(imageKey);

                return NotFound(new RestResponse
                {
                    Status = RestStatus.NotFound,
                    Meta = new RestMeta
                    {
                        ApiName = "CategoryImage",
                        Links =
                    {
                        ["self"] = $"/api/categories/{id}/image"
                    }
                    },
                    Data = new
                    {
                        message = "Active category was not found."
                    }
                });
            }
        }
        catch
        {
            // Remove the newly uploaded file if database persistence fails.
            await TryDeleteImageAsync(imageKey);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(result.PreviousImageKey) &&
            !string.Equals(
                result.PreviousImageKey,
                imageKey,
                StringComparison.Ordinal) &&
            IsLocalStorageImageKey(result.PreviousImageKey))
        {
            // The database now references the new image.
            // Failure to remove the old image must not invalidate the update.
            await TryDeleteImageAsync(result.PreviousImageKey);
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "CategoryImage",
                DataType = "application/json",
                Manipulations = ["POST"],
                Links =
            {
                ["parent"] = "/api/categories",
                ["image"] = $"/api/categories/images/{imageKey}"
            }
            },
            Data = new
            {
                categoryId = id,
                imageKey,
                imageUrl = $"/api/categories/images/{imageKey}"
            }
        });
    }



    private static bool IsLocalStorageImageKey(string imageKey)
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

    private async Task TryDeleteImageAsync(string imageKey)
    {
        try
        {
            await _localStorage.DeleteAsync(
                imageKey,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Cleanup failure must not hide the original result or exception.
            _logger.LogWarning(
                exception,
                "Failed to remove category image {ImageKey}.",
                imageKey);
        }
    }
}




using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Mvc;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController(DataAccessor dataAccessor) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;

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
        if (!await _dataAccessor.CategoryExistsAsync(id))
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "CategoryProducts",
                    Links = { ["self"] = $"/api/categories/{id}/products" }
                },
                Data = new { message = "Category not found." }
            });
        }

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
                Links = { ["self"] = $"/api/categories/{id}/products" },
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
} 

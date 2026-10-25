using Microsoft.AspNetCore.Authorization;
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Brands;
using foxtrot_store_backend.Models.Products;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Mvc;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/brands")]
public class BrandsController(DataAccessor dataAccessor) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;

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
        if (!await _dataAccessor.BrandExistsAsync(id))
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "BrandProducts",
                    Links = { ["self"] = $"/api/brands/{id}/products" }
                },
                Data = new { message = "Brand not found." }
            });
        }

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
                Links = { ["self"] = $"/api/brands/{id}/products" },
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
                Meta = new RestMeta { ApiName = "Brand" },
                Data = new { message = "Brand not found." }
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta { ApiName = "Brand" },
            Data = new { message = "Brand deleted successfully." }
        });
    }

}

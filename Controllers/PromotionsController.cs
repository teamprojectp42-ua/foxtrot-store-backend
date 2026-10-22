
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Mvc;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/promotions")]
public class PromotionsController(DataAccessor dataAccessor) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;

    [HttpGet]
    public async Task<IActionResult> GetPromotions()
    {
        var promotions = await _dataAccessor.GetPromotionsAsync();

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Promotions",
                ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DataType = "application/json",
                Manipulations = ["GET"]
            },
            Data = promotions
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPromotionById(Guid id)
    {
        var promotion = await _dataAccessor.GetPromotionByIdAsync(id);

        if (promotion is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Promotions",
                    ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DataType = "application/json",
                    Manipulations = ["GET"]
                },
                Data = null
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Promotions",
                ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DataType = "application/json",
                Manipulations = ["GET"]
            },
            Data = promotion
        });
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetPromotionBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 200)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Meta = new RestMeta
                {
                    ApiName = "Promotions",
                    ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DataType = "application/json",
                    Manipulations = ["GET"]
                },
                Data = null
            });
        }

        var promotion = await _dataAccessor.GetPromotionBySlugAsync(slug);

        if (promotion is null)
        {
            return NotFound(new RestResponse
            {
                Status = RestStatus.NotFound,
                Meta = new RestMeta
                {
                    ApiName = "Promotions",
                    ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    DataType = "application/json",
                    Manipulations = ["GET"]
                },
                Data = null
            });
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "Promotions",
                ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DataType = "application/json",
                Manipulations = ["GET"]
            },
            Data = promotion
        });
    }
}

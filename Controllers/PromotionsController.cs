using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Promotions;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Mvc;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/promotions")]
public class PromotionsController(DataAccessor dataAccessor) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;

    // Public endpoints

    [HttpGet]
    public async Task<IActionResult> GetPromotions()
    {
        var promotions = await _dataAccessor.GetPromotionsAsync();

        return Ok(CreateResponse(RestStatus.Ok, promotions));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPromotionById(Guid id)
    {
        var promotion = await _dataAccessor.GetPromotionByIdAsync(id);

        if (promotion is null)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new { message = "Promotion not found." }));
        }

        return Ok(CreateResponse(RestStatus.Ok, promotion));
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetPromotionBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 200)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "Invalid promotion slug." }));
        }

        var promotion = await _dataAccessor.GetPromotionBySlugAsync(slug);

        if (promotion is null)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new { message = "Promotion not found." }));
        }

        return Ok(CreateResponse(RestStatus.Ok, promotion));
    }

    // Admin endpoints

    [HttpGet("admin")]
    public async Task<IActionResult> GetPromotionsForAdmin()
    {
        var promotions = await _dataAccessor.GetPromotionsForAdminAsync();

        return Ok(CreateResponse(RestStatus.Ok, promotions));
    }

    [HttpGet("admin/{id:guid}")]
    public async Task<IActionResult> GetPromotionForAdminById(Guid id)
    {
        var promotion =
            await _dataAccessor.GetPromotionForAdminByIdAsync(id);

        if (promotion is null)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new { message = "Promotion not found." }));
        }

        return Ok(CreateResponse(RestStatus.Ok, promotion));
    }

    [HttpPost]
    public async Task<IActionResult> CreatePromotion(
        [FromBody] PromotionCreateRequest request)
    {
        try
        {
            var promotion =
                await _dataAccessor.CreatePromotionAsync(request);

            return CreatedAtAction(
                nameof(GetPromotionForAdminById),
                new { id = promotion.Id },
                CreateResponse(RestStatus.Created, promotion));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(CreateResponse(
                RestStatus.Conflict,
                new { message = ex.Message }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = ex.Message }));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePromotion(
        Guid id,
        [FromBody] PromotionUpdateRequest request)
    {
        try
        {
            var promotion =
                await _dataAccessor.UpdatePromotionAsync(id, request);

            if (promotion is null)
            {
                return NotFound(CreateResponse(
                    RestStatus.NotFound,
                    new { message = "Promotion not found." }));
            }

            return Ok(CreateResponse(RestStatus.Ok, promotion));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(CreateResponse(
                RestStatus.Conflict,
                new { message = ex.Message }));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = ex.Message }));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePromotion(Guid id)
    {
        var deleted = await _dataAccessor.DeletePromotionAsync(id);

        if (!deleted)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new { message = "Promotion not found." }));
        }

        return Ok(CreateResponse(
            RestStatus.Ok,
            new { message = "Promotion deactivated successfully." }));
    }

    private static RestResponse CreateResponse(
        RestStatus status,
        object? data)
    {
        return new RestResponse
        {
            Status = status,
            Meta = new RestMeta
            {
                ApiName = "Promotions",
                ServerTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DataType = "application/json",
                Manipulations = ["GET", "POST", "PUT", "DELETE"]
            },
            Data = data
        };
    }
}

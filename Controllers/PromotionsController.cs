using foxtrot_store_backend.Data;
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Promotions;
using foxtrot_store_backend.Models.Rest;
using foxtrot_store_backend.Services.LocalStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/promotions")]
public class PromotionsController(
    DataAccessor dataAccessor,
    ILocalStorageService localStorage,
    ILogger<PromotionsController> logger) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;
    private readonly ILocalStorageService _localStorage = localStorage;
    private readonly ILogger<PromotionsController> _logger = logger;

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

    [HttpGet("images/{imageKey}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPromotionImage(
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
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "Unsupported image format." }));
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
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "Invalid image key." }));
        }

        if (imageStream is null)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new { message = "Image not found." }));
        }

        return File(imageStream, contentType);
    }

    // Admin endpoints

    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public async Task<IActionResult> GetPromotionsForAdmin()
    {
        var promotions = await _dataAccessor.GetPromotionsForAdminAsync();

        return Ok(CreateResponse(RestStatus.Ok, promotions));
    }

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> UploadPromotionImage(
    Guid id,
    [FromForm] IFormFile? file,
    CancellationToken cancellationToken)
    {
        return SavePromotionImageAsync(
            id,
            file,
            PromotionImageType.Details,
            cancellationToken);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/banner")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> UploadPromotionBanner(
    Guid id,
    [FromForm] IFormFile? file,
    CancellationToken cancellationToken)
    {
        return SavePromotionImageAsync(
            id,
            file,
            PromotionImageType.Banner,
            cancellationToken);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/card-image")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> UploadPromotionCardImage(
    Guid id,
    [FromForm] IFormFile? file,
    CancellationToken cancellationToken)
    {
        return SavePromotionImageAsync(
            id,
            file,
            PromotionImageType.Card,
            cancellationToken);
    }

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestorePromotion(Guid id)
    {
        var restored = await _dataAccessor.RestorePromotionAsync(id);

        if (!restored)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new
                {
                    message = "Inactive promotion not found or already active."
                }));
        }

        return Ok(CreateResponse(
            RestStatus.Ok,
            new
            {
                message = "Promotion restored successfully."
            }));
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
    private async Task<IActionResult> SavePromotionImageAsync(
        Guid id,
        IFormFile? file,
        PromotionImageType imageType,
        CancellationToken cancellationToken)
    {
        var imageName = imageType switch
        {
            PromotionImageType.Banner => "banner",
            PromotionImageType.Card => "card image",
            PromotionImageType.Details => "image",
            _ => throw new ArgumentOutOfRangeException(nameof(imageType))
        };

        if (file is null || file.Length == 0)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new
                {
                    message = $"A promotion {imageName} file is required."
                }));
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
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = exception.Message }));
        }

        (bool Updated, string? PreviousImageKey) result;

        try
        {
            result = await _dataAccessor.SetPromotionImageAsync(
                id,
                imageKey,
                imageType,
                cancellationToken);

            if (!result.Updated)
            {
                await TryDeletePromotionImageAsync(imageKey);

                return NotFound(CreateResponse(
                    RestStatus.NotFound,
                    new { message = "Promotion not found." }));
            }
        }
        catch
        {
            await TryDeletePromotionImageAsync(imageKey);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(result.PreviousImageKey) &&
            !string.Equals(
                result.PreviousImageKey,
                imageKey,
                StringComparison.Ordinal) &&
            IsLocalStoragePromotionImageKey(result.PreviousImageKey))
        {
            await TryDeletePromotionImageAsync(result.PreviousImageKey);
        }

        var imageUrl = $"/api/promotions/images/{imageKey}";

        var responseField = imageType switch
        {
            PromotionImageType.Banner => "bannerImageUrl",
            PromotionImageType.Card => "cardImageUrl",
            PromotionImageType.Details => "imageUrl",
            _ => throw new ArgumentOutOfRangeException(nameof(imageType))
        };

        var data = new Dictionary<string, object>
        {
            ["promotionId"] = id,
            ["imageKey"] = imageKey,
            [responseField] = imageUrl
        };

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Meta = new RestMeta
            {
                ApiName = "PromotionImage",
                DataType = "application/json",
                Manipulations = ["POST"],
                Links =
            {
                ["parent"] = "/api/promotions",
                ["image"] = imageUrl
            }
            },
            Data = data
        });
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
    private async Task TryDeletePromotionImageAsync(string imageKey)
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
                "Failed to remove promotion image {ImageKey}.",
                imageKey);
        }
    }
}

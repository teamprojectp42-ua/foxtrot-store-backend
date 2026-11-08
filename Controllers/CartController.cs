using foxtrot_store_backend.Constants;
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Cart;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Roles = AppRoles.User)]
public class CartController(DataAccessor dataAccessor) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;
    private string CurrentCultureCode =>
        CultureInfo.CurrentUICulture.Name;

    // GET: api/cart
    [HttpGet]
    public async Task<IActionResult> GetCart(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(CreateResponse(
                RestStatus.Unauthorized,
                new { message = "User identity could not be determined." },
                "GET"));
        }

        var cart = await _dataAccessor.GetCartAsync(
            userId,
            CurrentCultureCode,
            cancellationToken);

        return Ok(CreateResponse(
            RestStatus.Ok,
            cart,
            "GET"));
    }

    // POST: api/cart/items
    [HttpPost("items")]
    public async Task<IActionResult> AddCartItem(
        [FromBody] CartItemAddRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(CreateResponse(
                RestStatus.Unauthorized,
                new { message = "User identity could not be determined." },
                "POST"));
        }

        if (request.ProductId == Guid.Empty)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "ProductId cannot be empty." },
                "POST"));
        }

        try
        {
            var cart = await _dataAccessor.AddCartItemAsync(
                userId,
                request.ProductId,
                request.Quantity,
                CurrentCultureCode,
                cancellationToken);

            return Ok(CreateResponse(
                RestStatus.Ok,
                cart,
                "POST"));
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            KeyNotFoundException or
            InvalidOperationException)
        {
            return HandleCartException(exception, "POST");
        }
    }

    // PUT: api/cart/items/{productId}
    [HttpPut("items/{productId:guid}")]
    public async Task<IActionResult> UpdateCartItemQuantity(
        Guid productId,
        [FromBody] CartItemQuantityUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(CreateResponse(
                RestStatus.Unauthorized,
                new { message = "User identity could not be determined." },
                "PUT"));
        }

        if (productId == Guid.Empty)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "ProductId cannot be empty." },
                "PUT"));
        }

        try
        {
            var cart = await _dataAccessor.UpdateCartItemQuantityAsync(
                userId,
                productId,
                request.Quantity,
                CurrentCultureCode,
                cancellationToken);

            if (cart is null)
            {
                return NotFound(CreateResponse(
                    RestStatus.NotFound,
                    new
                    {
                        message = "Cart or cart item not found."
                    },
                    "PUT"));
            }

            return Ok(CreateResponse(
                RestStatus.Ok,
                cart,
                "PUT"));
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            KeyNotFoundException or
            InvalidOperationException)
        {
            return HandleCartException(exception, "PUT");
        }
    }

    // DELETE: api/cart/items/{productId}
    [HttpDelete("items/{productId:guid}")]
    public async Task<IActionResult> RemoveCartItem(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(CreateResponse(
                RestStatus.Unauthorized,
                new { message = "User identity could not be determined." },
                "DELETE"));
        }

        var cart = await _dataAccessor.RemoveCartItemAsync(
            userId,
            productId,
            CurrentCultureCode,
            cancellationToken);

        if (cart is null)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new { message = "Cart item not found." },
                "DELETE"));
        }

        return Ok(CreateResponse(
            RestStatus.Ok,
            cart,
            "DELETE"));
    }

    // DELETE: api/cart
    [HttpDelete]
    public async Task<IActionResult> ClearCart(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(CreateResponse(
                RestStatus.Unauthorized,
                new { message = "User identity could not be determined." },
                "DELETE"));
        }

        var cart = await _dataAccessor.ClearCartAsync(
            userId,
            CurrentCultureCode,
            cancellationToken);

        return Ok(CreateResponse(
            RestStatus.Ok,
            cart,
            "DELETE"));
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private IActionResult HandleCartException(
        Exception exception,
        string method)
    {
        return exception switch
        {
            ArgumentException =>
                BadRequest(CreateResponse(
                    RestStatus.BadRequest,
                    new { message = exception.Message },
                    method)),

            KeyNotFoundException =>
                NotFound(CreateResponse(
                    RestStatus.NotFound,
                    new { message = exception.Message },
                    method)),

            InvalidOperationException =>
                Conflict(CreateResponse(
                    RestStatus.Conflict,
                    new { message = exception.Message },
                    method)),

            _ => throw exception
        };
    }

    private static RestResponse CreateResponse(
        RestStatus status,
        object? data,
        string method)
    {
        return new RestResponse
        {
            Status = status,
            Meta = new RestMeta
            {
                ApiName = "Cart",
                DataType = "application/json",
                CacheTime = 0,
                Manipulations = [method],
                Links =
                {
                    ["self"] = "/api/cart"
                }
            },
            Data = data
        };
    }
}

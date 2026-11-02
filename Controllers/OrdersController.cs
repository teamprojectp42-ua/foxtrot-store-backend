using foxtrot_store_backend.Constants;
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Data.Entities;
using foxtrot_store_backend.Models.Orders;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(DataAccessor dataAccessor) : ControllerBase
{
    private readonly DataAccessor _dataAccessor = dataAccessor;

    // POST: api/orders
    // Create an order from the authenticated user's cart.
    [HttpPost]
    [Authorize(Roles = AppRoles.User)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
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

        try
        {
            var order = await _dataAccessor.CreateOrderAsync(
                userId,
                request,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                CreateResponse(
                    RestStatus.Created,
                    order,
                    "POST",
                    "/api/orders/my"));
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            KeyNotFoundException)
        {
            return HandleOrderException(exception, "POST");
        }
    }

    // GET: api/orders/my
    // Get only the authenticated user's orders.
    [HttpGet("my")]
    [Authorize(Roles = AppRoles.User)]
    public async Task<IActionResult> GetMyOrders(
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

        var orders = await _dataAccessor.GetMyOrdersAsync(
            userId,
            cancellationToken);

        return Ok(CreateResponse(
            RestStatus.Ok,
            orders,
            "GET",
            "/api/orders/my"));
    }

    // GET: api/orders/admin
    // Get all orders. Available to Admin and Manager.
    [HttpGet("admin")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager)]
    public async Task<IActionResult> GetAllOrders(
        CancellationToken cancellationToken)
    {
        var orders = await _dataAccessor.GetAllOrdersAsync(
            cancellationToken);

        return Ok(CreateResponse(
            RestStatus.Ok,
            orders,
            "GET",
            "/api/orders/admin"));
    }

    // GET: api/orders/admin/{id}
    // Get order details. Available to Admin and Manager.
    [HttpGet("admin/{id:guid}")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager)]
    public async Task<IActionResult> GetOrderById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "Order ID cannot be empty." },
                "GET"));
        }

        var order = await _dataAccessor.GetOrderByIdAsync(
            id,
            cancellationToken);

        if (order is null)
        {
            return NotFound(CreateResponse(
                RestStatus.NotFound,
                new { message = "Order not found." },
                "GET"));
        }

        return Ok(CreateResponse(
            RestStatus.Ok,
            order,
            "GET",
            $"/api/orders/admin/{id}"));
    }

    // PUT: api/orders/admin/{id}/status
    // Update order status. Available to Admin and Manager.
    [HttpPut("admin/{id:guid}/status")]
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager)]
    public async Task<IActionResult> UpdateOrderStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "Order ID cannot be empty." },
                "PUT"));
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new { message = "Status is required." },
                "PUT"));
        }

        var statusName = request.Status.Trim();

        // Accept only defined enum names, not numeric values.
        var allowedStatuses = Enum.GetNames<OrderStatus>();

        if (!allowedStatuses.Contains(
                statusName,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(CreateResponse(
                RestStatus.BadRequest,
                new
                {
                    message =
                        "Invalid status. Allowed values: " +
                        "New, Processing, Completed, Cancelled."
                },
                "PUT"));
        }

        Enum.TryParse<OrderStatus>(
            statusName,
            true,
            out var newStatus);

        try
        {
            var order = await _dataAccessor.UpdateOrderStatusAsync(
                id,
                newStatus,
                cancellationToken);

            if (order is null)
            {
                return NotFound(CreateResponse(
                    RestStatus.NotFound,
                    new { message = "Order not found." },
                    "PUT"));
            }

            return Ok(CreateResponse(
                RestStatus.Ok,
                order,
                "PUT",
                $"/api/orders/admin/{id}"));
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException)
        {
            return HandleOrderException(exception, "PUT");
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private IActionResult HandleOrderException(
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
        string method,
        string self = "/api/orders")
    {
        return new RestResponse
        {
            Status = status,
            Meta = new RestMeta
            {
                ApiName = "Orders",
                DataType = "application/json",
                CacheTime = 0,
                Manipulations = [method],
                Links =
                {
                    ["self"] = self
                }
            },
            Data = data
        };
    }
}
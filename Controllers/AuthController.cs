using foxtrot_store_backend.Constants;
using foxtrot_store_backend.Data;
using foxtrot_store_backend.Models.Auth;
using foxtrot_store_backend.Models.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace foxtrot_store_backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    DataAccessor dataAccessor,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IConfiguration configuration) : ControllerBase
{

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var (user, result) = await dataAccessor.CreateUserAsync(
            request.UserName,
            request.Email,
            request.Password);

        if (!result.Succeeded)
        {
            return IdentityErrorResponse(result);
        }

        return StatusCode(201, new RestResponse
        {
            Status = RestStatus.Created,
            Data = await dataAccessor.GetUserByIdAsync(user!.Id)
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await dataAccessor.FindUserByEmailAsync(request.Email);

        if (user is null)
        {
            return Unauthorized(new RestResponse
            {
                Status = RestStatus.Unauthorized,
                Data = new { message = "Invalid email or password." }
            });
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return Unauthorized(new RestResponse
            {
                Status = RestStatus.Unauthorized,
                Data = new { message = "Invalid email or password." }
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var token = CreateAccessToken(user, roles, expiresAt);

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = new AuthResponseDto
            {
                AccessToken = token,
                ExpiresAtUtc = expiresAt,
                User = await dataAccessor.GetUserByIdAsync(user.Id) ?? new UserDto()
            }
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var user = await dataAccessor.GetUserByIdAsync(userId);

        if (user is null)
            return NotFound();

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = user
        });
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe(UserProfileUpdateRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var result = await dataAccessor.UpdateUserProfileAsync(
            userId,
            request.UserName,
            request.Email);

        if (!result.Succeeded)
            return IdentityErrorResponse(result);

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = await dataAccessor.GetUserByIdAsync(userId)
        });
    }

    [HttpPut("me/password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var result = await dataAccessor.ChangeUserPasswordAsync(
            userId,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
            return IdentityErrorResponse(result);

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = new { message = "Password changed successfully." }
        });
    }

    [HttpGet("users")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GetUsers()
    {
        var users = await dataAccessor.GetUsersAsync();

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = users
        });
    }

    [HttpGet("users/{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await dataAccessor.GetUserByIdAsync(id);

        if (user is null)
            return NotFound();

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = user
        });
    }

    [HttpPost("users")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CreateUser(UserCreateRequest request)
    {
        var (user, result) = await dataAccessor.CreateUserAsync(
            request.UserName,
            request.Email,
            request.Password,
            request.Role);

        if (!result.Succeeded)
            return IdentityErrorResponse(result);

        return StatusCode(201, new RestResponse
        {
            Status = RestStatus.Created,
            Data = await dataAccessor.GetUserByIdAsync(user!.Id)
        });
    }

    [HttpPut("users/{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateUser(
        string id,
        UserUpdateRequest request)
    {
        var result = await dataAccessor.UpdateUserAsync(
            id,
            request.UserName,
            request.Email);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == "UserNotFound"))
                return NotFound();

            return IdentityErrorResponse(result);
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = await dataAccessor.GetUserByIdAsync(id)
        });
    }

    [HttpPut("users/{id}/role")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> UpdateUserRole(
    string id,
    UpdateUserRoleRequest request)
    {
        var result = await dataAccessor.UpdateUserRoleAsync(
            id,
            request.Role);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code == "UserNotFound"))
                return NotFound();

            return IdentityErrorResponse(result);
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = await dataAccessor.GetUserByIdAsync(id)
        });
    }

    [HttpDelete("users/{id}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var currentUserId = GetCurrentUserId();

        if (currentUserId == id)
        {
            return BadRequest(new RestResponse
            {
                Status = RestStatus.BadRequest,
                Data = new
                {
                    message = "You cannot delete your own account through this endpoint."
                }
            });
        }

        var result = await dataAccessor.DeleteUserAsync(id);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code == "UserNotFound"))
                return NotFound();

            return IdentityErrorResponse(result);
        }

        return Ok(new RestResponse
        {
            Status = RestStatus.Ok,
            Data = new { message = "User deleted successfully." }
        });
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
    }

    private string CreateAccessToken(
        IdentityUser user,
        IEnumerable<string> roles,
        DateTime expiresAt)
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is not configured.");

        var issuer = configuration["Jwt:Issuer"] ?? "FoxtrotStore";
        var audience = configuration["Jwt:Audience"] ?? "FoxtrotStoreClient";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("unique_name", user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private IActionResult IdentityErrorResponse(IdentityResult result)
    {
        var errors = result.Errors
            .Select(error => new
            {
                error.Code,
                error.Description
            })
            .ToArray();

        var hasConflict = result.Errors.Any(error =>
            error.Code is "DuplicateUserName"
                or "DuplicateEmail"
                or "LastAdmin");

        if (hasConflict)
        {
            return Conflict(new { errors });
        }

        return BadRequest(new { errors });
    }
}

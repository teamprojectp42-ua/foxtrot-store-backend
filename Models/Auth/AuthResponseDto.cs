
namespace foxtrot_store_backend.Models.Auth;

public sealed class AuthResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public DateTime ExpiresAtUtc { get; set; }

    public UserDto User { get; set; } = new();
}

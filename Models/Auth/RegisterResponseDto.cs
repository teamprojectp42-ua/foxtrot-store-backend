namespace foxtrot_store_backend.Models.Auth;

public class RegisterResponseDto
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
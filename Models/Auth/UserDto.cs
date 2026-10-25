
namespace foxtrot_store_backend.Models.Auth;

public sealed class UserDto
{
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }

    public bool IsLockedOut { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();
}

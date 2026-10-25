using foxtrot_store_backend.Constants;

namespace foxtrot_store_backend.Models.Auth;

public class UpdateUserRoleRequest
{
    public string Role { get; set; } = AppRoles.User;
}
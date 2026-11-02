namespace foxtrot_store_backend.Constants;

public static class AppRoles
{
    public const string User = "User";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static readonly string[] All =
    [
        User,
        Manager,
        Admin
    ];
}
using foxtrot_store_backend.Constants;
using foxtrot_store_backend.Data;
using Microsoft.AspNetCore.Identity;

namespace foxtrot_store_backend.Services;

public sealed class IdentityInitializer(
    DataAccessor dataAccessor,
    UserManager<IdentityUser> userManager,
    IConfiguration configuration,
    ILogger<IdentityInitializer> logger)
{
    public async Task InitializeAsync()
    {
        // Ensure that all application roles exist.
        await dataAccessor.EnsureRolesCreatedAsync();

        // Do not create another administrator if one already exists.
        var administrators = await userManager.GetUsersInRoleAsync(
            AppRoles.Admin);

        if (administrators.Count > 0)
        {
            logger.LogInformation(
                "An administrator already exists.");

            return;
        }

        // Read initial administrator credentials from configuration.
        var userName = configuration["AdminBootstrap:UserName"];
        var email = configuration["AdminBootstrap:Email"];
        var password = configuration["AdminBootstrap:Password"];

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "No administrator exists. Configure " +
                "AdminBootstrap:UserName, AdminBootstrap:Email " +
                "and AdminBootstrap:Password.");
        }

        userName = userName.Trim();
        email = email.Trim();

        // Never promote an existing account automatically.
        var existingEmail = await userManager.FindByEmailAsync(email);
        var existingUserName = await userManager.FindByNameAsync(userName);

        if (existingEmail is not null ||
            existingUserName is not null)
        {
            throw new InvalidOperationException(
                "The configured administrator credentials conflict " +
                "with an existing account. No role changes were made.");
        }

        // Create the first administrator through ASP.NET Core Identity.
        var (user, result) = await dataAccessor.CreateUserAsync(
            userName,
            email,
            password,
            AppRoles.Admin);

        if (!result.Succeeded || user is null)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error =>
                    $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException(
                $"Failed to create the initial administrator: {errors}");
        }

        logger.LogInformation(
            "The initial administrator was created successfully.");
    }
}
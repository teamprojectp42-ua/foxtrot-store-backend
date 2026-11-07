using foxtrot_store_backend;
using foxtrot_store_backend.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace foxtrot_store_backend.Services.Localization;

public sealed class LocalizedIdentityErrorDescriber
    : IdentityErrorDescriber
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public LocalizedIdentityErrorDescriber(
        IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
    }

    private IdentityError Create(
        string errorCode,
        params object[] arguments)
    {
        var key = $"Identity_{errorCode}";

        return new IdentityError
        {
            Code = errorCode,
            Description = _localizer[key, arguments].Value
        };
    }

    public override IdentityError DefaultError()
        => Create(nameof(DefaultError));

    public override IdentityError ConcurrencyFailure()
        => Create(nameof(ConcurrencyFailure));

    public override IdentityError PasswordMismatch()
        => Create(nameof(PasswordMismatch));

    public override IdentityError InvalidToken()
        => Create(nameof(InvalidToken));

    public override IdentityError RecoveryCodeRedemptionFailed()
        => Create(nameof(RecoveryCodeRedemptionFailed));

    public override IdentityError PasswordTooShort(int length)
        => Create(nameof(PasswordTooShort), length);

    public override IdentityError PasswordRequiresNonAlphanumeric()
        => Create(nameof(PasswordRequiresNonAlphanumeric));

    public override IdentityError PasswordRequiresDigit()
        => Create(nameof(PasswordRequiresDigit));

    public override IdentityError PasswordRequiresLower()
        => Create(nameof(PasswordRequiresLower));

    public override IdentityError PasswordRequiresUpper()
        => Create(nameof(PasswordRequiresUpper));

    public override IdentityError PasswordRequiresUniqueChars(
        int uniqueChars)
        => Create(nameof(PasswordRequiresUniqueChars), uniqueChars);

    public override IdentityError DuplicateUserName(string? userName)
        => Create(nameof(DuplicateUserName), userName ?? string.Empty);

    public override IdentityError DuplicateEmail(string? email)
        => Create(nameof(DuplicateEmail), email ?? string.Empty);

    public override IdentityError InvalidEmail(string? email)
        => Create(nameof(InvalidEmail), email ?? string.Empty);

    public override IdentityError InvalidRoleName(string? role)
        => Create(nameof(InvalidRoleName), role ?? string.Empty);

    public override IdentityError DuplicateRoleName(string? role)
        => Create(nameof(DuplicateRoleName), role ?? string.Empty);

    public override IdentityError UserAlreadyHasPassword()
        => Create(nameof(UserAlreadyHasPassword));

    public override IdentityError UserLockoutNotEnabled()
        => Create(nameof(UserLockoutNotEnabled));

    public override IdentityError UserAlreadyInRole(string? role)
        => Create(nameof(UserAlreadyInRole), role ?? string.Empty);

    public override IdentityError UserNotInRole(string? role)
        => Create(nameof(UserNotInRole), role ?? string.Empty);

    public override IdentityError LoginAlreadyAssociated()
        => Create(nameof(LoginAlreadyAssociated));
}
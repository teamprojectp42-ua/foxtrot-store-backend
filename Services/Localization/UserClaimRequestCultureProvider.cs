using Microsoft.AspNetCore.Localization;

namespace foxtrot_store_backend.Services.Localization;

public sealed class UserClaimRequestCultureProvider
    : RequestCultureProvider
{
    private static readonly HashSet<string> SupportedCultures =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "uk-UA",
            "en-US"
        };

    public override Task<ProviderCultureResult?>
        DetermineProviderCultureResult(HttpContext httpContext)
    {
        var cultureCode =
            httpContext.User.FindFirst("locale")?.Value;

        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        if (!SupportedCultures.Contains(cultureCode.Trim()))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        var normalizedCulture =
            SupportedCultures.First(
                culture => string.Equals(
                    culture,
                    cultureCode.Trim(),
                    StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<ProviderCultureResult?>(
            new ProviderCultureResult(normalizedCulture));
    }
}
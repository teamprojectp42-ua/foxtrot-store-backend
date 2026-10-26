namespace foxtrot_store_backend.Services.LocalStorage;

public static class LocalStorageServiceExtension
{
    public static IServiceCollection AddLocalStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<LocalStorageOptions>()
            .Bind(configuration.GetSection(
                LocalStorageOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RootPath),
                "LocalStorage RootPath must be configured.")
            .Validate(
                options => options.MaxFileSizeBytes > 0,
                "MaxFileSizeBytes must be greater than zero.")
            .Validate(
                options => options.MaxImagePixels > 0,
                "MaxImagePixels must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<
            ILocalStorageService,
            LocalStorageService>();

        return services;
    }
}
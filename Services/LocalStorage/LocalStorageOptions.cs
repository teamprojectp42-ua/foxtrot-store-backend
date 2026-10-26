namespace foxtrot_store_backend.Services.LocalStorage;

public sealed class LocalStorageOptions
{
    public const string SectionName = "LocalStorage";

    public string RootPath { get; set; } = "LocalStorage/Files";

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public long MaxImagePixels { get; set; } = 12_000_000;
}
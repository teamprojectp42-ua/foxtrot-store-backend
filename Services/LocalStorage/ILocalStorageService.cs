namespace foxtrot_store_backend.Services.LocalStorage;

public interface ILocalStorageService
{
    /// <summary>
    /// Validates, normalizes and stores an image.
    /// Returns a relative storage key for database persistence.
    /// </summary>
    Task<string> SaveAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stored file or returns null if it does not exist.
    /// </summary>
    Task<Stream?> GetAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a stored file.
    /// Returns false if the file does not exist.
    /// </summary>
    Task<bool> DeleteAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
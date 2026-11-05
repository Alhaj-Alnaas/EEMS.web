namespace Core.Interfaces.Services.Organization
{
    /// <summary>Stores employee badge photos under wwwroot for later card printing.</summary>
    public interface IEmployeePhotoService
    {
        /// <summary>
        /// Saves a JPEG (or other image) for the given file number.
        /// Returns a web-relative path like /uploads/employees/{fileNumber}.jpg
        /// </summary>
        Task<string> SavePhotoAsync(string fileNumber, byte[] imageBytes, string? contentType = null, CancellationToken ct = default);

        /// <summary>Deletes a previously stored photo if the path is under the uploads folder.</summary>
        void TryDeletePhoto(string? photoPath);

        /// <summary>
        /// Reads a stored photo as a data URL (data:image/...;base64,...) suitable for print HTML.
        /// Returns null if the path is missing or the file cannot be read.
        /// </summary>
        string? TryGetPhotoDataUrl(string? photoPath);
    }
}

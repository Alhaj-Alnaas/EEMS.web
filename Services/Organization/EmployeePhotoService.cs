using Core.Interfaces.Services.Organization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services.Organization
{
    public class EmployeePhotoService : IEmployeePhotoService
    {
        private readonly IHostEnvironment _env;
        private readonly ILogger<EmployeePhotoService> _logger;

        public EmployeePhotoService(IHostEnvironment env, ILogger<EmployeePhotoService> logger)
        {
            _env = env;
            _logger = logger;
        }

        private string WebRootPath
        {
            get
            {
                var primary = Path.Combine(_env.ContentRootPath, "wwwroot");
                if (Directory.Exists(primary))
                    return primary;

                var alt = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                return Directory.Exists(alt) ? alt : primary;
            }
        }

        public async Task<string> SavePhotoAsync(
            string fileNumber,
            byte[] imageBytes,
            string? contentType = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(fileNumber))
                throw new ArgumentException("File number is required.", nameof(fileNumber));
            if (imageBytes is null || imageBytes.Length == 0)
                throw new ArgumentException("Image data is empty.", nameof(imageBytes));

            var safeName = string.Concat(fileNumber.Trim().Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
            if (string.IsNullOrEmpty(safeName))
                safeName = Guid.NewGuid().ToString("N");

            var ext = ResolveExtension(contentType);
            var relativeDir = Path.Combine("uploads", "employees");
            var physicalDir = Path.Combine(WebRootPath, relativeDir);
            Directory.CreateDirectory(physicalDir);

            var fileName = $"{safeName}{ext}";
            var physicalPath = Path.Combine(physicalDir, fileName);
            await File.WriteAllBytesAsync(physicalPath, imageBytes, ct);

            return "/" + relativeDir.Replace('\\', '/') + "/" + fileName;
        }

        public void TryDeletePhoto(string? photoPath)
        {
            if (string.IsNullOrWhiteSpace(photoPath))
                return;

            try
            {
                var physical = ResolvePhysicalPath(photoPath);
                if (physical != null && File.Exists(physical))
                    File.Delete(physical);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete employee photo {Path}", photoPath);
            }
        }

        public string? TryGetPhotoDataUrl(string? photoPath)
        {
            try
            {
                var physical = ResolvePhysicalPath(photoPath);
                if (physical == null || !File.Exists(physical))
                    return null;

                var bytes = File.ReadAllBytes(physical);
                if (bytes.Length == 0)
                    return null;

                var mime = ResolveMimeFromExtension(Path.GetExtension(physical));
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read employee photo {Path}", photoPath);
                return null;
            }
        }

        private string? ResolvePhysicalPath(string? photoPath)
        {
            if (string.IsNullOrWhiteSpace(photoPath))
                return null;

            // Strip query string (e.g. ?t=ticks used for cache busting in UI)
            var pathOnly = photoPath.Split('?', 2)[0].Replace('\\', '/').TrimStart('/');
            if (!pathOnly.StartsWith("uploads/employees/", StringComparison.OrdinalIgnoreCase))
                return null;

            return Path.Combine(WebRootPath, pathOnly.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolveExtension(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return ".jpg";

            return contentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "image/jpeg" or "image/jpg" => ".jpg",
                _ => ".jpg"
            };
        }

        private static string ResolveMimeFromExtension(string? ext)
        {
            return (ext ?? "").ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
        }
    }
}

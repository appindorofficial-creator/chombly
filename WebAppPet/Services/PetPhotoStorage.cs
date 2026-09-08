using Microsoft.AspNetCore.Hosting;

namespace WebAppPet.Services;

/// <summary>Guarda fotos de mascota bajo uploads/pets/ con validación de tipo y tamaño.</summary>
public static class PetPhotoStorage
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/jpg",
        // Algunos móviles envían esto al elegir de galería
        "application/octet-stream", "image/*"
    };

    /// <summary>Returns localization key on failure, or null if OK / no file.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return null;

        if (file.Length > MaxBytes)
            return "Pets_PhotoInvalid";

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return "Pets_PhotoInvalid";

        // Reject path tricks in the client filename (we never use it as a path segment).
        if (file.FileName.Contains("..", StringComparison.Ordinal) ||
            file.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Pets_PhotoInvalid";

        var contentType = file.ContentType?.Trim() ?? "";
        // Extensión válida basta: en prod/móvil el Content-Type a veces viene vacío o genérico.
        if (!string.IsNullOrEmpty(contentType) &&
            !AllowedContentTypes.Contains(contentType) &&
            !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "Pets_PhotoInvalid";

        return null;
    }

    /// <summary>Saves the file and returns a site-relative URL like /uploads/pets/1_guid.jpg.</summary>
    public static async Task<string> SaveAsync(
        IFormFile file,
        int userId,
        IWebHostEnvironment env,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";

        var safeName = $"{userId}_{Guid.NewGuid():N}{ext}";
        var absoluteDir = UploadPaths.GetAbsoluteDir(env, "uploads", "pets");
        var absolutePath = Path.Combine(absoluteDir, safeName);

        // Ensure resolved path stays under uploads/pets (no traversal).
        var fullDir = Path.GetFullPath(absoluteDir);
        var fullFile = Path.GetFullPath(absolutePath);
        if (!fullFile.StartsWith(fullDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid upload path.");

        await using (var stream = new FileStream(fullFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return "/uploads/pets/" + safeName;
    }
}

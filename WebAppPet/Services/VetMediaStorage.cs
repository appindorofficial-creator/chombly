using Microsoft.AspNetCore.Hosting;

namespace WebAppPet.Services;

/// <summary>Local uploads for vet consultation media under wwwroot/uploads/vet/.</summary>
public static class VetMediaStorage
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const long MaxVideoBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".webm"
    };

    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return null;

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext))
            return "Vet_MediaInvalid";

        var isImage = ImageExt.Contains(ext);
        var isVideo = VideoExt.Contains(ext);
        if (!isImage && !isVideo)
            return "Vet_MediaInvalid";

        if (isImage && file.Length > MaxImageBytes)
            return "Vet_MediaInvalid";
        if (isVideo && file.Length > MaxVideoBytes)
            return "Vet_MediaInvalid";

        if (file.FileName.Contains("..", StringComparison.Ordinal) ||
            file.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Vet_MediaInvalid";

        return null;
    }

    public static async Task<string> SaveAsync(
        IFormFile file,
        int userId,
        IWebHostEnvironment env,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";

        var safeName = $"{userId}_{Guid.NewGuid():N}{ext}";
        var absoluteDir = UploadPaths.GetAbsoluteDir(env, "uploads", "vet");
        var absolutePath = Path.Combine(absoluteDir, safeName);
        var fullDir = Path.GetFullPath(absoluteDir);
        var fullFile = Path.GetFullPath(absolutePath);
        if (!fullFile.StartsWith(fullDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid upload path.");

        await using (var stream = new FileStream(fullFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return "/uploads/vet/" + safeName;
    }
}

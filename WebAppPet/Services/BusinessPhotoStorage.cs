using Microsoft.AspNetCore.Hosting;

namespace WebAppPet.Services;

/// <summary>Guarda logo/portada de negocio bajo uploads/business/.</summary>
public static class BusinessPhotoStorage
{
    public const long MaxBytes = PetPhotoStorage.MaxBytes;

    public static string? Validate(IFormFile? file) => PetPhotoStorage.Validate(file);

    public static async Task<string> SaveAsync(
        IFormFile file,
        int? userId,
        IWebHostEnvironment env,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";

        var prefix = userId is > 0 ? $"{userId}_" : "";
        var safeName = $"{prefix}{Guid.NewGuid():N}{ext}";
        var absoluteDir = UploadPaths.GetAbsoluteDir(env, "uploads", "business");
        var absolutePath = Path.Combine(absoluteDir, safeName);

        var fullDir = Path.GetFullPath(absoluteDir);
        var fullFile = Path.GetFullPath(absolutePath);
        if (!fullFile.StartsWith(fullDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid upload path.");

        await using (var stream = new FileStream(fullFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return "/uploads/business/" + safeName;
    }
}

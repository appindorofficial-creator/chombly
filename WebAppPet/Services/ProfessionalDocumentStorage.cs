using Microsoft.AspNetCore.Hosting;

namespace WebAppPet.Services;

/// <summary>Professional onboarding proofs (licenses, certificates) under uploads/professional/.</summary>
public static class ProfessionalDocumentStorage
{
    private const int MaxExtensionLength = 10;

    public static async Task<string> SaveAsync(IFormFile file, int userId, IWebHostEnvironment env, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(file.FileName);
        if (ext.Length > MaxExtensionLength) ext = ".bin";

        var name = $"{userId}_{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(UploadPaths.GetAbsoluteDir(env, "uploads", "professional"), name);
        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream, ct);
        }

        return "/uploads/professional/" + name;
    }
}

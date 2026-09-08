using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace WebAppPet.Services;

/// <summary>
/// Raíz persistente para uploads. En Azure App Service usa %HOME%/data
/// (fuera de site/wwwroot) para que el zip deploy no borre las fotos.
/// En local usa wwwroot.
/// </summary>
public static class UploadPaths
{
    public static string GetDataRoot(IWebHostEnvironment env)
    {
        var configured = Environment.GetEnvironmentVariable("UPLOAD_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var home = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(home))
            return Path.GetFullPath(Path.Combine(home, "data"));

        return Path.GetFullPath(env.WebRootPath);
    }

    public static string GetAbsoluteDir(IWebHostEnvironment env, params string[] relativeSegments)
    {
        var parts = new string[relativeSegments.Length + 1];
        parts[0] = GetDataRoot(env);
        Array.Copy(relativeSegments, 0, parts, 1, relativeSegments.Length);
        var dir = Path.Combine(parts);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void MapUploadStaticFiles(WebApplication app)
    {
        var dataRoot = GetDataRoot(app.Environment);
        var uploadsRoot = Path.Combine(dataRoot, "uploads");
        Directory.CreateDirectory(uploadsRoot);

        // wwwroot (css/js/imágenes del sitio)
        app.UseStaticFiles();

        // /uploads/* desde carpeta persistente (Azure HOME/data/uploads o wwwroot/uploads en local)
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsRoot),
            RequestPath = "/uploads"
        });
    }
}

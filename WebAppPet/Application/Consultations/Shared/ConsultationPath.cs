using WebAppPet.Models;

namespace WebAppPet.Application.Consultations.Shared;

/// <summary>
/// Which virtual path the client is on: "local" is the US state-licensed teleconsult (VCPR),
/// "intl" is general guidance with international vets.
/// </summary>
public static class ConsultationPath
{
    public const string Local = "local";
    public const string Intl = "intl";

    /// <summary>The requested path, or the one implied by the consultation's service.</summary>
    public static string Normalize(string? next, string? catalogCode)
    {
        if (string.Equals(next, Local, StringComparison.OrdinalIgnoreCase)) return Local;
        if (string.Equals(next, Intl, StringComparison.OrdinalIgnoreCase)) return Intl;
        return string.Equals(catalogCode, ServiceCatalogCodes.VetLocal30, StringComparison.OrdinalIgnoreCase)
            ? Local
            : Intl;
    }
}

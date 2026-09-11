namespace WebAppPet.Services;

/// <summary>Resolves category / business thumbnails to the current flat Chombly icons.</summary>
public static class CatalogImages
{
    public static string Category(string? slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? "/images/pet-other.svg"
            : $"/images/categories/cat-{slug.Trim().ToLowerInvariant()}-live.png";

    /// <summary>
    /// Prefer a real photo; if missing or a stale category placeholder, use the business category icon.
    /// </summary>
    public static string Business(string? imageUrl, string? categorySlug)
    {
        var fallback = Category(categorySlug);
        if (string.IsNullOrWhiteSpace(imageUrl))
            return fallback;

        var url = imageUrl.Trim();
        // Seed / old rows often stored a generic grooming category asset for every business.
        if (url.Contains("/images/categories/cat-", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/images/icons/svc-", StringComparison.OrdinalIgnoreCase)
            || url.Contains("/images/icons/ico-", StringComparison.OrdinalIgnoreCase))
            return fallback;

        return url;
    }

    public static string PopularService(string? name)
    {
        var n = (name ?? "").Trim().ToLowerInvariant();
        if (n.Length == 0) return Category("grooming");

        if (ContainsAny(n, "urgencia", "emergencia", "emergency", "consulta", "vacun", "vet",
                "orientaci", "internacional", "teleconsulta", "clínic", "clinic"))
            return Category("vet");

        if (ContainsAny(n, "comport", "conducta", "behavior", "entren", "cachorr", "obedien", "train", "sesión", "sesion"))
            return Category("trainers");

        if (ContainsAny(n, "noche", "hotel", "board", "hosped"))
            return Category("hotel");

        if (ContainsAny(n, "guardería", "guarderia", "daycare", "día completo", "dia completo"))
            return Category("daycare");

        if (ContainsAny(n, "paseo", "walk"))
            return Category("walkers");

        if (ContainsAny(n, "baño", "bano", "cepill", "bath", "deshedd", "muda", "pelo", "corte", "clip", "estilo", "cut", "groom"))
            return Category("grooming");

        return Category("grooming");
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}

using WebAppPet.Models;

namespace WebAppPet.Application.Businesses.CreateBusiness;

/// <summary>
/// Suggested services for the registration wizard. Names are stored in Spanish, the catalog language,
/// and CatalogLocalizer shows them in English when needed.
/// </summary>
public static class BusinessServiceDefaults
{
    private static readonly Dictionary<string, string> EnglishToSpanish = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Standard night"] = "Noche estándar",
        ["Premium night"] = "Noche premium",
        ["General checkup"] = "Consulta general",
        ["Vaccines"] = "Vacunas",
        ["Full day"] = "Día completo",
        ["Half day"] = "Medio día",
        ["30-min walk"] = "Paseo 30 min",
        ["60-min walk"] = "Paseo 60 min",
        ["Basic obedience"] = "Obediencia básica",
        ["Advanced session"] = "Sesión avanzada",
        ["Basic bath"] = "Baño básico",
        ["Haircut"] = "Corte de pelo",
        ["Full grooming"] = "Grooming completo"
    };

    private static readonly HashSet<string> Placeholders =
        new(EnglishToSpanish.Keys.Concat(EnglishToSpanish.Values), StringComparer.OrdinalIgnoreCase);

    /// <summary>Suggestions for the selected categories without duplicates. Falls back to grooming.</summary>
    public static (List<string> Names, List<decimal> Prices) ForCategories(IEnumerable<ServiceCategory> selected)
    {
        var names = new List<string>();
        var prices = new List<decimal>();
        foreach (var category in selected)
        {
            var (n, p) = ForSlug(category.Slug);
            for (var i = 0; i < n.Count; i++)
            {
                if (names.Contains(n[i], StringComparer.OrdinalIgnoreCase)) continue;
                names.Add(n[i]);
                prices.Add(p[i]);
            }
        }

        return names.Count > 0 ? (names, prices) : ForSlug("grooming");
    }

    /// <summary>True when every name is one of the canned suggestions, in either language.</summary>
    public static bool ArePlaceholders(IReadOnlyCollection<string?> names) =>
        names.Count > 0 && names.All(n => Placeholders.Contains(n?.Trim() ?? ""));

    /// <summary>Turns English suggestions back into Spanish when the list only has suggestions.</summary>
    public static void NormalizePlaceholdersToSpanish(List<string> names)
    {
        if (!ArePlaceholders(names)) return;
        for (var i = 0; i < names.Count; i++)
        {
            var trimmed = (names[i] ?? "").Trim();
            names[i] = EnglishToSpanish.TryGetValue(trimmed, out var spanish) ? spanish : trimmed;
        }
    }

    /// <summary>Drops rows without a name and pairs each name with its price (0 when missing).</summary>
    public static (List<string> Names, List<decimal> Prices) Normalize(IReadOnlyList<string?>? names, IReadOnlyList<decimal>? prices)
    {
        names ??= [];
        prices ??= [];
        var outNames = new List<string>();
        var outPrices = new List<decimal>();
        var n = Math.Max(names.Count, prices.Count);
        for (var i = 0; i < n; i++)
        {
            var name = i < names.Count ? names[i]?.Trim() ?? "" : "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            outNames.Add(name);
            outPrices.Add(i < prices.Count ? prices[i] : 0);
        }
        return (outNames, outPrices);
    }

    private static (List<string> Names, List<decimal> Prices) ForSlug(string slug) => slug switch
    {
        "hotel" => (["Noche estándar", "Noche premium"], [45, 60]),
        "vet" => (["Consulta general", "Vacunas"], [50, 35]),
        "daycare" => (["Día completo", "Medio día"], [35, 25]),
        "walkers" => (["Paseo 30 min", "Paseo 60 min"], [15, 25]),
        "trainers" => (["Obediencia básica", "Sesión avanzada"], [45, 60]),
        _ => (["Baño básico", "Corte de pelo", "Grooming completo"], [35, 45, 55])
    };
}

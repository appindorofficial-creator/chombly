using System.Globalization;

namespace WebAppPet.Models;

/// <summary>Catálogos de raza y temperamento para el alta de mascotas (valores canónicos en ES).</summary>
public static class PetCatalog
{
    public static readonly IReadOnlyList<(string Value, string LabelEn)> Temperaments = new[]
    {
        ("Amigable", "Friendly"),
        ("Tranquilo", "Calm"),
        ("Enérgico", "Energetic"),
        ("Juguetón", "Playful"),
        ("Tímido", "Shy"),
        ("Independiente", "Independent")
    };

    /// <summary>Valor especial: muestra input libre de raza.</summary>
    public const string OtherBreed = "__other__";

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<(string Value, string LabelEn)>> Breeds =
        new Dictionary<string, IReadOnlyList<(string Value, string LabelEn)>>(StringComparer.OrdinalIgnoreCase)
        {
            [PetSpecies.Dog] = new[]
            {
                ("Mestizo / mixto", "Mixed / mix"),
                ("Labrador Retriever", "Labrador Retriever"),
                ("Golden Retriever", "Golden Retriever"),
                ("Pastor alemán", "German Shepherd"),
                ("Bulldog", "Bulldog"),
                ("Beagle", "Beagle"),
                ("Poodle", "Poodle"),
                ("Chihuahua", "Chihuahua"),
                ("Yorkshire Terrier", "Yorkshire Terrier"),
                ("Boxer", "Boxer"),
                ("Dachshund", "Dachshund"),
                ("Husky siberiano", "Siberian Husky"),
                ("Shih Tzu", "Shih Tzu"),
                ("Cocker Spaniel", "Cocker Spaniel"),
                ("Rottweiler", "Rottweiler"),
                ("Border Collie", "Border Collie"),
                ("French Bulldog", "French Bulldog"),
                ("Pitbull / American Staffordshire", "Pit Bull / American Staffordshire"),
            },
            [PetSpecies.Cat] = new[]
            {
                ("Doméstico de pelo corto", "Domestic shorthair"),
                ("Doméstico de pelo largo", "Domestic longhair"),
                ("Mestizo / mixto", "Mixed / mix"),
                ("Persa", "Persian"),
                ("Siamés", "Siamese"),
                ("Maine Coon", "Maine Coon"),
                ("Ragdoll", "Ragdoll"),
                ("Bengalí", "Bengal"),
                ("British Shorthair", "British Shorthair"),
                ("Sphynx", "Sphynx"),
                ("Abisinio", "Abyssinian"),
                ("Scottish Fold", "Scottish Fold"),
                ("Russian Blue", "Russian Blue"),
            },
            [PetSpecies.Rabbit] = new[]
            {
                ("Mestizo / mixto", "Mixed / mix"),
                ("Holland Lop", "Holland Lop"),
                ("Rex", "Rex"),
                ("Enano holandés", "Netherland Dwarf"),
                ("Mini Lop", "Mini Lop"),
                ("Angora", "Angora"),
                ("Lionhead", "Lionhead"),
            },
            [PetSpecies.GuineaPig] = new[]
            {
                ("Americana", "American"),
                ("Abisinia", "Abyssinian"),
                ("Peruana", "Peruvian"),
                ("Teddy", "Teddy"),
                ("Skinny", "Skinny"),
                ("Mestiza / mixtas", "Mixed"),
            },
            [PetSpecies.Bird] = new[]
            {
                ("Periquito", "Budgie"),
                ("Cacatúa", "Cockatoo"),
                ("Agapornis", "Lovebird"),
                ("Canario", "Canary"),
                ("Ninfa / Cockatiel", "Cockatiel"),
                ("Loro amazónico", "Amazon parrot"),
            },
            [PetSpecies.Ferret] = new[]
            {
                ("Estándar / sable", "Standard / sable"),
                ("Albino", "Albino"),
                ("Canela", "Cinnamon"),
                ("Chocolate", "Chocolate"),
                ("Champagne", "Champagne"),
                ("Negro", "Black"),
                ("Panda / blaze", "Panda / blaze"),
                ("Angora", "Angora"),
                ("Mestizo / mixto", "Mixed / mix"),
            }
        };

    public static IReadOnlyList<(string Value, string LabelEn)> BreedsFor(string? species)
    {
        if (string.IsNullOrWhiteSpace(species)) return Array.Empty<(string, string)>();
        if (!Breeds.TryGetValue(species, out var list)) return Array.Empty<(string, string)>();
        // Una sola entrada por valor (case-insensitive) por especie.
        return list
            .GroupBy(b => b.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    public static bool HasBreedList(string? species) =>
        !string.IsNullOrWhiteSpace(species)
        && species != PetSpecies.Other
        && Breeds.ContainsKey(species);

    public static string Display(string value, string labelEn) =>
        IsEnglish() ? labelEn : value;

    public static string DisplayTemperament(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? "";
        var hit = Temperaments.FirstOrDefault(t => t.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        return hit.Value == null ? value : Display(hit.Value, hit.LabelEn);
    }

    public static string DisplayBreed(string? species, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value ?? "";
        var hit = BreedsFor(species).FirstOrDefault(b => b.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        return hit.Value == null ? value : Display(hit.Value, hit.LabelEn);
    }

    private static bool IsEnglish() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .StartsWith("en", StringComparison.OrdinalIgnoreCase);
}

using System.Globalization;

namespace WebAppPet.Models;

/// <summary>Mascotas más comunes en EE.UU. para grooming / pet care.</summary>
public static class PetSpecies
{
    public const string Dog = "Perro";
    public const string Cat = "Gato";
    public const string Rabbit = "Conejo";
    public const string GuineaPig = "Cobaya";
    public const string Bird = "Ave";
    public const string Ferret = "Hurón";
    public const string Other = "Otro";

    public static readonly IReadOnlyList<(string Value, string Label, string LabelEn, string Emoji, string BreedHint, string BreedHintEn)> All = new[]
    {
        (Dog, "Perro", "Dog", "🐕", "Golden Retriever, Labrador, Mestizo…", "Golden Retriever, Labrador, Mixed…"),
        (Cat, "Gato", "Cat", "🐈", "Persa, Siamés, Doméstico de pelo corto…", "Persian, Siamese, Domestic shorthair…"),
        (Rabbit, "Conejo", "Rabbit", "🐇", "Holland Lop, Rex, Enano…", "Holland Lop, Rex, Dwarf…"),
        (GuineaPig, "Cobaya", "Guinea pig", "🐹", "Americana, Abisinia, Peruana…", "American, Abyssinian, Peruvian…"),
        (Bird, "Ave", "Bird", "🦜", "Periquito, Cacatúa, Agapornis…", "Budgie, Cockatoo, Lovebird…"),
        (Ferret, "Hurón", "Ferret", "🦡", "Estándar, Albino, Angora…", "Standard, Albino, Angora…"),
        (Other, "Otro", "Other", "🐾", "Raza o tipo de tu mascota", "Breed or type of your pet")
    };

    public static string DefaultAcceptedList => string.Join(",", All.Select(x => x.Value));

    public static string Emoji(string? species) =>
        All.FirstOrDefault(x => x.Value.Equals(species, StringComparison.OrdinalIgnoreCase)).Emoji
        ?? "🐾";

    public static string Label(string? species)
    {
        if (string.IsNullOrWhiteSpace(species)) return DisplayLabel(Dog);
        var item = All.FirstOrDefault(x => x.Value.Equals(species, StringComparison.OrdinalIgnoreCase));
        if (item.Value == null) return species;
        return DisplayLabel(item.Value);
    }

    public static string DisplayLabel(string value)
    {
        var item = All.FirstOrDefault(x => x.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (item.Value == null) return value;
        return IsEnglish() ? item.LabelEn : item.Label;
    }

    public static string BreedHint(string? species)
    {
        var item = All.FirstOrDefault(x => x.Value.Equals(species ?? Dog, StringComparison.OrdinalIgnoreCase));
        if (item.Value == null) return "";
        return IsEnglish() ? item.BreedHintEn : item.BreedHint;
    }

    public static string DefaultPhoto(string? species) => species switch
    {
        Cat => "/images/pet-cat.svg",
        Rabbit => "/images/pet-rabbit.svg",
        GuineaPig => "/images/pet-guineapig.svg",
        Bird => "/images/pet-bird.svg",
        Ferret => "/images/pet-ferret.svg",
        Other => "/images/pet-other.svg",
        Dog => "/images/pet-dog.svg",
        _ => "/images/pet-other.svg"
    };

    public static string IconPath(string? species) => DefaultPhoto(species);

    public static string PhotoOrDefault(string? photoUrl, string? species) =>
        string.IsNullOrWhiteSpace(photoUrl) ? DefaultPhoto(species) : photoUrl;

    public static bool IsKnown(string? species) =>
        !string.IsNullOrWhiteSpace(species) &&
        All.Any(x => x.Value.Equals(species, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> ParseList(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return All.Select(x => x.Value);

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }

    public static bool ListIncludes(string? csv, string? species)
    {
        if (string.IsNullOrWhiteSpace(species)) return true;
        if (string.IsNullOrWhiteSpace(csv)) return true;
        return ParseList(csv).Any(s => s.Equals(species, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEnglish() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
            .StartsWith("en", StringComparison.OrdinalIgnoreCase);
}

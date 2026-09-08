using System.Text.RegularExpressions;

namespace WebAppPet.Services;

/// <summary>
/// Valida teléfonos: solo dígitos y formato (+, espacios, guiones, paréntesis).
/// Rechaza letras y símbolos como $ &amp; ) sueltos sin contexto válido.
/// </summary>
public static partial class PhoneValidator
{
    /// <summary>
    /// Valida y normaliza. Si <paramref name="phone"/> está vacío, es válido (opcional)
    /// salvo que <paramref name="required"/> sea true.
    /// </summary>
    public static bool TryNormalize(string? phone, out string? normalized, bool required = false)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(phone))
            return !required;

        var trimmed = phone.Trim();

        // Solo: opcional +, dígitos, espacios, guiones, puntos, paréntesis
        if (!AllowedChars().IsMatch(trimmed))
            return false;

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.Length is < 7 or > 15)
            return false;

        // No permitir que haya letras/símbolos raros ya cubierto; normalizar a E.164-ish
        normalized = trimmed.StartsWith('+') ? "+" + digits : digits;
        return true;
    }

    public static bool IsValid(string? phone, bool required = false) =>
        TryNormalize(phone, out _, required);

    [GeneratedRegex(@"^\+?[\d\s\-().]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedChars();
}

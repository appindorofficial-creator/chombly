using System.Globalization;
using System.Text.RegularExpressions;

namespace WebAppPet.Services;

/// <summary>
/// Format validation for simulated card storage (Luhn, expiry, CVV, holder).
/// Does not contact banks or payment providers.
/// </summary>
public static partial class CardValidator
{
    public const int MinCardDigits = 13;
    public const int MaxCardDigits = 19;
    public const int MinHolderLength = 2;
    public const int MaxHolderLength = 100;

    public enum CardBrand
    {
        Unknown,
        Visa,
        Mastercard,
        Amex,
        Discover
    }

    public enum FailReason
    {
        None,
        CardNumberRequired,
        CardNumberInvalid,
        CardNumberLuhn,
        ExpiryRequired,
        ExpiryFormat,
        ExpiryMonth,
        ExpiryExpired,
        CvvRequired,
        CvvInvalid,
        HolderRequired,
        HolderInvalid
    }

    public sealed class Result
    {
        public bool Ok { get; init; }
        public FailReason Reason { get; init; }
        public string Digits { get; init; } = string.Empty;
        public CardBrand Brand { get; init; }
        public string BrandName { get; init; } = "Tarjeta";
        public string Last4 { get; init; } = string.Empty;
        public int ExpMonth { get; init; }
        public int ExpYear { get; init; }
        public string HolderName { get; init; } = string.Empty;
        public int ExpectedCvvLength { get; init; } = 3;
    }

    public static string DigitsOnly(string? value) =>
        new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public static bool PassesLuhn(string digits)
    {
        if (digits.Length is < MinCardDigits or > MaxCardDigits)
            return false;
        if (!digits.All(char.IsDigit))
            return false;

        var sum = 0;
        var alt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (alt)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alt = !alt;
        }
        return sum % 10 == 0;
    }

    public static CardBrand DetectBrand(string digits)
    {
        if (string.IsNullOrEmpty(digits))
            return CardBrand.Unknown;

        // American Express: 34 or 37
        if (digits.StartsWith("34", StringComparison.Ordinal) || digits.StartsWith("37", StringComparison.Ordinal))
            return CardBrand.Amex;

        // Visa: 4…
        if (digits[0] == '4')
            return CardBrand.Visa;

        // Mastercard: 51–55 or 2221–2720
        if (digits.Length >= 2)
        {
            if (int.TryParse(digits.AsSpan(0, 2), out var two) && two is >= 51 and <= 55)
                return CardBrand.Mastercard;
        }
        if (digits.Length >= 4 && int.TryParse(digits.AsSpan(0, 4), out var four) && four is >= 2221 and <= 2720)
            return CardBrand.Mastercard;

        // Discover: 6011, 65, 644–649, 622126–622925
        if (digits.StartsWith("6011", StringComparison.Ordinal) || digits.StartsWith("65", StringComparison.Ordinal))
            return CardBrand.Discover;
        if (digits.Length >= 3 && int.TryParse(digits.AsSpan(0, 3), out var three) && three is >= 644 and <= 649)
            return CardBrand.Discover;
        if (digits.Length >= 6 && int.TryParse(digits.AsSpan(0, 6), out var six) && six is >= 622126 and <= 622925)
            return CardBrand.Discover;

        return CardBrand.Unknown;
    }

    public static string BrandDisplayName(CardBrand brand) => brand switch
    {
        CardBrand.Visa => "Visa",
        CardBrand.Mastercard => "Mastercard",
        CardBrand.Amex => "Amex",
        CardBrand.Discover => "Discover",
        _ => "Tarjeta"
    };

    public static int CvvLengthFor(CardBrand brand) => brand == CardBrand.Amex ? 4 : 3;

    public static bool TryParseExpiry(string? expiry, out int month, out int year, out FailReason reason)
    {
        month = 0;
        year = 0;
        reason = FailReason.None;

        if (string.IsNullOrWhiteSpace(expiry))
        {
            reason = FailReason.ExpiryRequired;
            return false;
        }

        var raw = expiry.Trim();
        // Accept MM/AA, MM/AAAA, MMAA, MM-AA
        raw = raw.Replace("-", "/").Replace(" ", "");
        string monthPart;
        string yearPart;

        if (raw.Contains('/'))
        {
            var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                reason = FailReason.ExpiryFormat;
                return false;
            }
            monthPart = parts[0];
            yearPart = parts[1];
        }
        else
        {
            var digits = DigitsOnly(raw);
            if (digits.Length is not (4 or 6))
            {
                reason = FailReason.ExpiryFormat;
                return false;
            }
            monthPart = digits[..2];
            yearPart = digits[2..];
        }

        if (!int.TryParse(monthPart, NumberStyles.None, CultureInfo.InvariantCulture, out month)
            || month is < 1 or > 12)
        {
            reason = FailReason.ExpiryMonth;
            return false;
        }

        if (!int.TryParse(yearPart, NumberStyles.None, CultureInfo.InvariantCulture, out year))
        {
            reason = FailReason.ExpiryFormat;
            return false;
        }

        if (year < 100) year += 2000;
        if (year is < 2000 or > 2100)
        {
            reason = FailReason.ExpiryFormat;
            return false;
        }

        // Valid through end of expiry month
        var lastDay = DateTime.DaysInMonth(year, month);
        var endOfMonth = new DateTime(year, month, lastDay, 23, 59, 59, DateTimeKind.Local);
        if (DateTime.Now > endOfMonth)
        {
            reason = FailReason.ExpiryExpired;
            return false;
        }

        return true;
    }

    public static bool IsValidHolderName(string? name, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var trimmed = HolderWhitespace().Replace(name.Trim(), " ");
        if (trimmed.Length is < MinHolderLength or > MaxHolderLength)
            return false;

        if (!HolderNamePattern().IsMatch(trimmed))
            return false;

        normalized = trimmed;
        return true;
    }

    public static Result Validate(string? cardNumber, string? expiry, string? cvv, string? holderName)
    {
        var digits = DigitsOnly(cardNumber);
        if (string.IsNullOrEmpty(digits))
            return Fail(FailReason.CardNumberRequired);

        if (digits.Length is < MinCardDigits or > MaxCardDigits || !digits.All(char.IsDigit))
            return Fail(FailReason.CardNumberInvalid);

        if (!PassesLuhn(digits))
            return Fail(FailReason.CardNumberLuhn);

        var brand = DetectBrand(digits);
        var expectedCvv = CvvLengthFor(brand);

        if (!TryParseExpiry(expiry, out var month, out var year, out var expiryReason))
            return Fail(expiryReason);

        var cvvRaw = (cvv ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cvvRaw))
            return Fail(FailReason.CvvRequired);

        var cvvDigits = DigitsOnly(cvvRaw);
        if (cvvDigits.Length != expectedCvv || cvvDigits != cvvRaw)
            return Fail(FailReason.CvvInvalid);

        if (string.IsNullOrWhiteSpace(holderName))
            return Fail(FailReason.HolderRequired);

        if (!IsValidHolderName(holderName, out var holder))
            return Fail(FailReason.HolderInvalid);

        return new Result
        {
            Ok = true,
            Reason = FailReason.None,
            Digits = digits,
            Brand = brand,
            BrandName = BrandDisplayName(brand),
            Last4 = digits[^4..],
            ExpMonth = month,
            ExpYear = year,
            HolderName = holder,
            ExpectedCvvLength = expectedCvv
        };
    }

    private static Result Fail(FailReason reason) => new() { Ok = false, Reason = reason };

    // Letters (incl. accents), spaces, and common name marks (', -, .)
    [GeneratedRegex(@"^[\p{L}'\.\-]+(?: [\p{L}'\.\-]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex HolderNamePattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex HolderWhitespace();
}

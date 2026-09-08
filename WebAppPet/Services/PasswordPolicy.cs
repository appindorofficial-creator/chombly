namespace WebAppPet.Services;

/// <summary>
/// Política de contraseñas nuevas: mín. 6, una mayúscula, un carácter especial; no solo dígitos.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 6;

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return false;

        if (password.All(char.IsDigit))
            return false;

        var hasUpper = false;
        var hasSpecial = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            if (hasUpper && hasSpecial) return true;
        }

        return false;
    }
}

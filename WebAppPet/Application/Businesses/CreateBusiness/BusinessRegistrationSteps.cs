using System.ComponentModel.DataAnnotations;
using WebAppPet.Infrastructure.Security;
using WebAppPet.Models;
using WebAppPet.Services;

namespace WebAppPet.Application.Businesses.CreateBusiness;

public enum RegistrationError
{
    None,
    ChooseType,
    NameRequired,
    BusinessNameRequired,
    PhoneRequired,
    PhoneInvalid,
    EmailInvalid,
    LocationRequired,
    PickService,
    OpenDay,
    Description,
    ServicePrice,
    AcceptTerms,
    PasswordWeak,
    PasswordMismatch,
    InvalidSession,
    AlreadyBusiness,
    EmailTaken,
    EmailTakenLogin,
    InvalidCategory
}

/// <summary>Contact and location data from step 1 of the registration wizard.</summary>
public sealed record BusinessBasics(
    string FullName,
    string BusinessName,
    string Phone,
    string Email,
    string City,
    double Latitude,
    double Longitude);

/// <summary>
/// Validation for each step of the business registration wizard: 0 type, 1 basics, 2 services,
/// 3 schedule, 4 photos and description, 5 prices, 6 account. Nothing here touches the database.
/// </summary>
public static class BusinessRegistrationSteps
{
    public const int MinAboutLength = 20;

    public static RegistrationError ValidateType(string? providerKindKey) =>
        providerKindKey is "business" or "independent" ? RegistrationError.None : RegistrationError.ChooseType;

    /// <returns>The trimmed values, with the phone normalized when it is valid.</returns>
    public static (RegistrationError Error, BusinessBasics Value) ValidateBasics(BusinessBasics input)
    {
        var value = input with
        {
            FullName = (input.FullName ?? "").Trim(),
            BusinessName = (input.BusinessName ?? "").Trim(),
            Email = (input.Email ?? "").Trim(),
            City = (input.City ?? "").Trim(),
            Phone = (input.Phone ?? "").Trim()
        };

        if (string.IsNullOrWhiteSpace(value.FullName))
            return (RegistrationError.NameRequired, value);
        if (string.IsNullOrWhiteSpace(value.BusinessName))
            return (RegistrationError.BusinessNameRequired, value);
        if (string.IsNullOrWhiteSpace(value.Phone))
            return (RegistrationError.PhoneRequired, value);
        if (!PhoneValidator.TryNormalize(value.Phone, out var phoneNorm, required: true))
            return (RegistrationError.PhoneInvalid, value);

        value = value with { Phone = phoneNorm! };
        if (string.IsNullOrWhiteSpace(value.Email) || !new EmailAddressAttribute().IsValid(value.Email))
            return (RegistrationError.EmailInvalid, value);
        if (string.IsNullOrWhiteSpace(value.City) || (value.Latitude == 0 && value.Longitude == 0))
            return (RegistrationError.LocationRequired, value);

        return (RegistrationError.None, value);
    }

    /// <param name="categoryIds">Selected ids, already de-duplicated.</param>
    public static RegistrationError ValidateCategories(IReadOnlyCollection<int> categoryIds, IEnumerable<ServiceCategory> activeCategories)
    {
        var activeIds = activeCategories.Select(c => c.Id).ToHashSet();
        return categoryIds.Count > 0 && categoryIds.All(activeIds.Contains)
            ? RegistrationError.None
            : RegistrationError.PickService;
    }

    public static RegistrationError ValidateSchedule(IEnumerable<WeekDayInput> week) =>
        week.Any(d => d.IsOpen) ? RegistrationError.None : RegistrationError.OpenDay;

    public static RegistrationError ValidateAbout(string? about) =>
        string.IsNullOrWhiteSpace(about) || about.Trim().Length < MinAboutLength
            ? RegistrationError.Description
            : RegistrationError.None;

    /// <summary>Signed-in users accept the terms here because they skip the account step.</summary>
    public static RegistrationError ValidatePrices(
        IReadOnlyCollection<string> serviceNames, IEnumerable<decimal> servicePrices, bool isSignedIn, bool acceptTerms)
    {
        if (serviceNames.Count == 0 || servicePrices.All(p => p <= 0))
            return RegistrationError.ServicePrice;
        if (isSignedIn && !acceptTerms)
            return RegistrationError.AcceptTerms;
        return RegistrationError.None;
    }

    public static RegistrationError ValidateAccount(bool isSignedIn, string? password, string? confirmPassword, bool acceptTerms)
    {
        if (!isSignedIn)
        {
            if (string.IsNullOrWhiteSpace(password) || !PasswordPolicy.IsValid(password))
                return RegistrationError.PasswordWeak;
            if (password != confirmPassword)
                return RegistrationError.PasswordMismatch;
        }
        return acceptTerms ? RegistrationError.None : RegistrationError.AcceptTerms;
    }
}

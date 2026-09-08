using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace WebAppPet.Localization;

/// <summary>
/// Localizes ASP.NET Core model-binding messages (otherwise they stay in English).
/// </summary>
public sealed class ConfigureMvcLocalization : IConfigureOptions<MvcOptions>
{
    private readonly IStringLocalizer<SharedResource> _L;

    public ConfigureMvcLocalization(IStringLocalizer<SharedResource> L) => _L = L;

    public void Configure(MvcOptions options)
    {
        // Non-nullable reference types otherwise get an implicit [Required] with English text.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

        var m = options.ModelBindingMessageProvider;
        m.SetValueIsInvalidAccessor(value =>
            _L["Validation_ValueInvalid", value ?? string.Empty].Value);
        m.SetAttemptedValueIsInvalidAccessor((value, field) =>
            _L["Validation_AttemptedValueInvalid", value ?? string.Empty, Field(field)].Value);
        m.SetValueMustBeANumberAccessor(field =>
            _L["Validation_MustBeNumber", Field(field)].Value);
        m.SetMissingBindRequiredValueAccessor(field =>
            _L["Validation_FieldRequired", Field(field)].Value);
        m.SetMissingKeyOrValueAccessor(() =>
            _L["Validation_Required"].Value);
        m.SetValueMustNotBeNullAccessor(field =>
            _L["Validation_FieldRequired", Field(field)].Value);
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(value =>
            _L["Validation_ValueInvalid", value ?? string.Empty].Value);
        m.SetNonPropertyValueMustBeANumberAccessor(() =>
            _L["Validation_MustBeNumberGeneric"].Value);
        m.SetUnknownValueIsInvalidAccessor(field =>
            _L["Validation_ValueInvalidGeneric", Field(field)].Value);
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() =>
            _L["Validation_ValueInvalid", string.Empty].Value);
    }

    private string Field(string name) => name switch
    {
        "Species" => _L["Pets_Species"].Value,
        "Name" => _L["Common_Name"].Value,
        "AgeYears" => _L["Pets_Age"].Value,
        "Size" => _L["Pets_Size"].Value,
        "CustomType" => _L["Pets_OtherType"].Value,
        "PhotoFile" => _L["Pets_Photo"].Value,
        "FullName" => _L["Register_FullName"].Value,
        "Email" => _L["Common_Email"].Value,
        "Password" => _L["Common_Password"].Value,
        "Phone" => _L["Common_Phone"].Value,
        _ => name
    };
}

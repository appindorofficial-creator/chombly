using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebAppPet.Ui.TagHelpers;

/// <summary>
/// Styled text input. Prefer asp-for on native inputs when model-bound;
/// use this for consistent chrome on free-form fields.
/// </summary>
[HtmlTargetElement("app-input", TagStructure = TagStructure.WithoutEndTag)]
public class AppInputTagHelper : TagHelper
{
    public string Type { get; set; } = "text";

    public string? Name { get; set; }

    public string? Value { get; set; }

    public string? Placeholder { get; set; }

    public string? Autocomplete { get; set; }

    public bool Disabled { get; set; }

    public AppAnimation Animation { get; set; } = AppAnimation.None;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "input";
        output.TagMode = TagMode.SelfClosing;
        output.Attributes.SetAttribute("type", Type);
        if (!string.IsNullOrWhiteSpace(Name))
            output.Attributes.SetAttribute("name", Name);
        if (Value != null)
            output.Attributes.SetAttribute("value", Value);
        if (!string.IsNullOrWhiteSpace(Placeholder))
            output.Attributes.SetAttribute("placeholder", Placeholder);
        if (!string.IsNullOrWhiteSpace(Autocomplete))
            output.Attributes.SetAttribute("autocomplete", Autocomplete);
        if (Disabled)
            output.Attributes.SetAttribute("disabled", "disabled");

        AppUiClasses.AppendClass(output, "form-control", "app-input", AppUiClasses.Animation(Animation));
    }
}

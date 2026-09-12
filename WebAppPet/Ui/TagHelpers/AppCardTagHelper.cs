using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebAppPet.Ui.TagHelpers;

/// <summary>
/// Reusable card shell. Example:
/// <c>&lt;app-card animation="ScaleIn"&gt;...&lt;/app-card&gt;</c>
/// </summary>
[HtmlTargetElement("app-card")]
public class AppCardTagHelper : TagHelper
{
    public AppAnimation Animation { get; set; } = AppAnimation.None;

    /// <summary>default | soft | flush</summary>
    public string Variant { get; set; } = "default";

    public string? Padding { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;

        var variant = Variant?.Trim().ToLowerInvariant() switch
        {
            "soft" => "app-card app-card--soft",
            "flush" => "app-card app-card--flush",
            _ => "app-card"
        };

        AppUiClasses.AppendClass(output, variant, AppUiClasses.Animation(Animation),
            string.IsNullOrWhiteSpace(Padding) ? null : $"app-card--pad-{Padding}");

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child);
    }
}

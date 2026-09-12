using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebAppPet.Ui.TagHelpers;

/// <summary>
/// Branded button with optional loading state.
/// <c>&lt;app-button variant="primary" loading="true"&gt;Guardar&lt;/app-button&gt;</c>
/// </summary>
[HtmlTargetElement("app-button")]
public class AppButtonTagHelper : TagHelper
{
    /// <summary>primary | outline | ghost | enter | danger</summary>
    public string Variant { get; set; } = "primary";

    public string Type { get; set; } = "button";

    public bool Loading { get; set; }

    public bool Block { get; set; }

    public bool Disabled { get; set; }

    public string? Size { get; set; }

    public AppAnimation Animation { get; set; } = AppAnimation.None;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", string.IsNullOrWhiteSpace(Type) ? "button" : Type);

        var variantClass = Variant?.Trim().ToLowerInvariant() switch
        {
            "outline" => "btn btn-outline",
            "ghost" => "btn btn-ghost",
            "enter" => "btn btn-enter",
            "danger" => "btn btn-outline app-btn--danger",
            _ => "btn btn-primary"
        };

        AppUiClasses.AppendClass(output, variantClass, "app-btn",
            Block ? "btn-block" : null,
            Size == "sm" ? "btn-sm" : null,
            Loading ? "is-loading" : null,
            AppUiClasses.Animation(Animation));

        if (Loading || Disabled)
        {
            output.Attributes.SetAttribute("disabled", "disabled");
            output.Attributes.SetAttribute("aria-busy", Loading ? "true" : "false");
        }

        var child = await output.GetChildContentAsync();
        if (Loading)
        {
            output.Content.SetHtmlContent(
                "<span class=\"app-btn-spinner\" aria-hidden=\"true\"></span>" +
                "<span class=\"app-btn-label\">" + child.GetContent() + "</span>");
        }
        else
        {
            output.Content.SetHtmlContent(child);
        }
    }
}

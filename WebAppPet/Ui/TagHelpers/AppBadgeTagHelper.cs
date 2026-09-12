using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebAppPet.Ui.TagHelpers;

/// <summary>
/// Numeric/status badge (e.g. unread count).
/// <c>&lt;app-badge count="3" /&gt;</c>
/// </summary>
[HtmlTargetElement("app-badge", TagStructure = TagStructure.NormalOrSelfClosing)]
public class AppBadgeTagHelper : TagHelper
{
    public int Count { get; set; }

    public int Max { get; set; } = 99;

    /// <summary>danger | lime | soft</summary>
    public string Variant { get; set; } = "danger";

    public bool Dot { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Count <= 0 && !Dot)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;

        var variant = Variant?.Trim().ToLowerInvariant() switch
        {
            "lime" => "app-badge app-badge--lime",
            "soft" => "app-badge app-badge--soft",
            _ => "app-badge app-badge--danger"
        };

        AppUiClasses.AppendClass(output, variant, Dot ? "app-badge--dot" : null, "app-anim-scale-in");
        output.Attributes.SetAttribute("aria-hidden", "true");

        if (Dot)
        {
            output.Content.SetContent(string.Empty);
            return;
        }

        output.Content.SetContent(Count > Max ? $"{Max}+" : Count.ToString());
    }
}

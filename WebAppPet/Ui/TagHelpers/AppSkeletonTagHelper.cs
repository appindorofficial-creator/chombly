using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebAppPet.Ui.TagHelpers;

/// <summary>
/// Loading placeholder block.
/// <c>&lt;app-skeleton lines="3" /&gt;</c>
/// </summary>
[HtmlTargetElement("app-skeleton", TagStructure = TagStructure.NormalOrSelfClosing)]
public class AppSkeletonTagHelper : TagHelper
{
    public int Lines { get; set; } = 3;

    /// <summary>text | card | avatar | row</summary>
    public string Variant { get; set; } = "text";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("aria-hidden", "true");

        var variant = Variant?.Trim().ToLowerInvariant() ?? "text";
        AppUiClasses.AppendClass(output, "app-skeleton", $"app-skeleton--{variant}", "app-anim-fade-in");

        if (variant is "avatar" or "card" or "row")
        {
            output.Content.SetHtmlContent("<span class=\"app-skeleton-shine\"></span>");
            return;
        }

        var lines = Math.Clamp(Lines, 1, 8);
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < lines; i++)
        {
            var w = i == lines - 1 ? "66%" : "100%";
            sb.Append($"<span class=\"app-skeleton-line\" style=\"width:{w}\"></span>");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}

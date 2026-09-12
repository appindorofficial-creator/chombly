using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebAppPet.Ui.TagHelpers;

/// <summary>
/// Lightweight tooltip via native title + accessible label.
/// <c>&lt;app-tooltip text="Guardar"&gt;&lt;button&gt;…&lt;/button&gt;&lt;/app-tooltip&gt;</c>
/// </summary>
[HtmlTargetElement("app-tooltip")]
public class AppTooltipTagHelper : TagHelper
{
    public string Text { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        AppUiClasses.AppendClass(output, "app-tooltip");
        if (!string.IsNullOrWhiteSpace(Text))
        {
            output.Attributes.SetAttribute("title", Text);
            output.Attributes.SetAttribute("data-tooltip", Text);
        }

        var child = await output.GetChildContentAsync();
        output.Content.SetHtmlContent(child);
    }
}

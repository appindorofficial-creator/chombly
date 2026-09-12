using Microsoft.AspNetCore.Razor.TagHelpers;

namespace WebAppPet.Ui.TagHelpers;

internal static class AppUiClasses
{
    public static string Animation(AppAnimation animation) => animation switch
    {
        AppAnimation.FadeIn => "app-anim-fade-in",
        AppAnimation.SlideIn => "app-anim-slide-in",
        AppAnimation.ScaleIn => "app-anim-scale-in",
        _ => string.Empty
    };

    public static void AppendClass(TagHelperOutput output, params string?[] parts)
    {
        var existing = output.Attributes["class"]?.Value?.ToString();
        var tokens = new List<string>();
        if (!string.IsNullOrWhiteSpace(existing))
            tokens.AddRange(existing.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            foreach (var piece in part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!tokens.Contains(piece, StringComparer.Ordinal))
                    tokens.Add(piece);
            }
        }

        if (tokens.Count == 0) return;
        output.Attributes.SetAttribute("class", string.Join(' ', tokens));
    }
}

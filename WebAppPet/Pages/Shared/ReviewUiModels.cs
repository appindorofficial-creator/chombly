using WebAppPet.Models;

namespace WebAppPet.Pages.Shared;

public sealed class ReviewsSectionModel
{
    public required IReadOnlyList<Review> Reviews { get; init; }
    public decimal Rating { get; init; }
    public int ReviewCount { get; init; }
    public string? WriteHref { get; init; }
}

public sealed class ReviewStarPickerModel
{
    public int Rating { get; init; }
    public string InputName { get; init; } = "Rating";
}

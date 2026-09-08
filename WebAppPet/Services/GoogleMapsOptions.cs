namespace WebAppPet.Services;

public class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    public string BrowserApiKey { get; set; } = "";
    public double DefaultLatitude { get; set; } = 35.2271;
    public double DefaultLongitude { get; set; } = -80.8431;
}

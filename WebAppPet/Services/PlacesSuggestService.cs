using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebAppPet.Services;

/// <summary>
/// City/address suggestions when Google Maps BrowserApiKey is not configured (local/dev).
/// Proxies OpenStreetMap Nominatim so the browser never hits CORS limits.
/// </summary>
public sealed class PlacesSuggestService
{
    private static readonly string[] AllowedCountries = ["us", "co", "mx", "sv", "es"];
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PlacesSuggestService> _logger;

    public PlacesSuggestService(IHttpClientFactory httpFactory, ILogger<PlacesSuggestService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlaceSuggestion>> SuggestCitiesAsync(string query, CancellationToken ct = default)
        => await SuggestAsync(query, cityOnly: true, ct);

    public async Task<IReadOnlyList<PlaceSuggestion>> SuggestAddressesAsync(string query, CancellationToken ct = default)
        => await SuggestAsync(query, cityOnly: false, ct);

    private async Task<IReadOnlyList<PlaceSuggestion>> SuggestAsync(string query, bool cityOnly, CancellationToken ct)
    {
        query = (query ?? "").Trim();
        if (query.Length < 2)
            return Array.Empty<PlaceSuggestion>();

        var client = _httpFactory.CreateClient("nominatim");
        var path = cityOnly
            ? $"search?format=jsonv2&addressdetails=1&limit=6&featureType=settlement&q={Uri.EscapeDataString(query)}&countrycodes={string.Join(',', AllowedCountries)}"
            : $"search?format=jsonv2&addressdetails=1&limit=6&q={Uri.EscapeDataString(query)}&countrycodes={string.Join(',', AllowedCountries)}";

        try
        {
            using var res = await client.GetAsync(path, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim {Status} for q={Query}", (int)res.StatusCode, query);
                return Array.Empty<PlaceSuggestion>();
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            var rows = await JsonSerializer.DeserializeAsync<List<NominatimRow>>(stream, cancellationToken: ct)
                       ?? new List<NominatimRow>();

            return rows
                .Select(MapRow)
                .Where(s => s is not null && !string.IsNullOrWhiteSpace(s!.Label))
                .Cast<PlaceSuggestion>()
                .GroupBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(6)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Nominatim suggest failed for q={Query}", query);
            return Array.Empty<PlaceSuggestion>();
        }
    }

    private static PlaceSuggestion? MapRow(NominatimRow row)
    {
        if (!double.TryParse(row.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
            return null;
        if (!double.TryParse(row.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
            return null;

        var addr = row.Address;
        var city = FirstNonEmpty(
            addr?.City, addr?.Town, addr?.Village, addr?.Municipality,
            addr?.County, addr?.Suburb, row.Name);
        var state = FirstNonEmpty(addr?.State, addr?.Region);
        var country = addr?.CountryCode?.ToUpperInvariant() ?? addr?.Country;

        var cityLabel = string.Join(", ", new[] { city, state, country }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(cityLabel))
            cityLabel = row.DisplayName ?? row.Name ?? "";

        var road = FirstNonEmpty(addr?.Road, addr?.Pedestrian, addr?.Neighbourhood);
        var house = addr?.HouseNumber;
        var addressLine = string.IsNullOrWhiteSpace(road)
            ? null
            : string.IsNullOrWhiteSpace(house) ? road : $"{road} {house}";

        return new PlaceSuggestion(
            Label: row.DisplayName ?? cityLabel,
            City: cityLabel,
            Address: addressLine,
            Lat: Math.Round(lat, 6),
            Lng: Math.Round(lng, 6));
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    public sealed record PlaceSuggestion(string Label, string City, string? Address, double Lat, double Lng);

    private sealed class NominatimRow
    {
        [JsonPropertyName("lat")] public string? Lat { get; set; }
        [JsonPropertyName("lon")] public string? Lon { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
        [JsonPropertyName("address")] public NominatimAddress? Address { get; set; }
    }

    private sealed class NominatimAddress
    {
        [JsonPropertyName("house_number")] public string? HouseNumber { get; set; }
        [JsonPropertyName("road")] public string? Road { get; set; }
        [JsonPropertyName("pedestrian")] public string? Pedestrian { get; set; }
        [JsonPropertyName("neighbourhood")] public string? Neighbourhood { get; set; }
        [JsonPropertyName("suburb")] public string? Suburb { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("town")] public string? Town { get; set; }
        [JsonPropertyName("village")] public string? Village { get; set; }
        [JsonPropertyName("municipality")] public string? Municipality { get; set; }
        [JsonPropertyName("county")] public string? County { get; set; }
        [JsonPropertyName("state")] public string? State { get; set; }
        [JsonPropertyName("region")] public string? Region { get; set; }
        [JsonPropertyName("country")] public string? Country { get; set; }
        [JsonPropertyName("country_code")] public string? CountryCode { get; set; }
    }
}

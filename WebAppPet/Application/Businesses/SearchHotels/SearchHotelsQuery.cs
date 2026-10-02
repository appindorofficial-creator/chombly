namespace WebAppPet.Application.Businesses.SearchHotels;

/// <param name="HotelCategoryId">Active hotel category; when null, falls back to the primary category slug.</param>
public sealed record SearchHotelsQuery(int? HotelCategoryId, string CountryCode);

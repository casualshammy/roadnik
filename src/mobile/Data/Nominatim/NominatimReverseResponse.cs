using System.Text.Json.Serialization;

namespace Roadnik.MAUI.Data.Nominatim;

internal sealed record NominatimReverseResponse(
  [property: JsonPropertyName("address")] NominatimAddress? Address);

internal sealed record NominatimAddress(
  [property: JsonPropertyName("city_district")] string? CityDistrict,
  [property: JsonPropertyName("suburb")] string? Suburb,
  [property: JsonPropertyName("city")] string? City,
  [property: JsonPropertyName("town")] string? Town,
  [property: JsonPropertyName("municipality")] string? Municipality,
  [property: JsonPropertyName("road")] string? Road);

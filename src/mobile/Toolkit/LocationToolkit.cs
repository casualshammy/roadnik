using Ax.Fw.Extensions;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using System.Text.Json;

namespace Roadnik.MAUI.Toolkit;

internal static class LocationToolkit
{
  private record LastApproxLocation(
    double Lat,
    double Lng,
    string Name);

  private static LastApproxLocation? p_lastApproxLocation;

  public static async Task<string?> TryGetApproximateLocationNameAsync(
    IHttpClientProvider _httpClientProvider,
    double _lat,
    double _lng,
    CancellationToken _ct)
  {
    var lastApproxLocation = p_lastApproxLocation;

    // Only re-geocode if moved more than ~1 km
    if (lastApproxLocation != null)
    {
      var dlat = (_lat - lastApproxLocation.Lat) * 111_000;
      var dlng = (_lng - lastApproxLocation.Lng) * 111_000 * Math.Cos(_lat * Math.PI / 180);
      if (Math.Sqrt(dlat * dlat + dlng * dlng) < 1000)
        return lastApproxLocation.Name;
    }

    try
    {
      var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={_lat.ToInvariantString()}&lon={_lng.ToInvariantString()}";
      using var req = new HttpRequestMessage(HttpMethod.Get, url);
      req.Headers.TryAddWithoutValidation("User-Agent", AppConsts.USER_AGENT);
      using var res = await _httpClientProvider.Value.SendAsync(req, _ct);
      res.EnsureSuccessStatusCode();

      using var stream = await res.Content.ReadAsStreamAsync(_ct);
      var data = await JsonSerializer.DeserializeAsync(stream, DiscordJsonCtx.Default.NominatimReverseResponse, _ct);

      var name = data?.Address?.CityDistrict
        ?? data?.Address?.Municipality
        ?? data?.Address?.Suburb
        ?? data?.Address?.Road
        ?? data?.Address?.City
        ?? data?.Address?.Town;

      name = name?
        .Replace("район", string.Empty, StringComparison.InvariantCultureIgnoreCase)
        .Trim();

      if (name != null)
        p_lastApproxLocation = new LastApproxLocation(_lat, _lng, name);

      return name;
    }
    catch
    {
      return null;
    }
  }
}

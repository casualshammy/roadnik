using Roadnik.MAUI.Data;
using Roadnik.MAUI.Toolkit;
using System.Globalization;

namespace Roadnik.MAUI.Pages.Parts;

internal class MapInteractor
{
  private readonly InteractableWebView p_webView;

  public MapInteractor(InteractableWebView _webView)
  {
    p_webView = _webView;
  }

  public async Task SetCurrentLocationAsync(
    LocationData _loc,
    float? _compassHeading,
    CancellationToken _ct)
  {
    _ct.ThrowIfCancellationRequested();

    var lat = _loc.Latitude.ToString(CultureInfo.InvariantCulture);
    var lng = _loc.Longitude.ToString(CultureInfo.InvariantCulture);
    var acc = _loc.Accuracy.ToString(CultureInfo.InvariantCulture);

    string arc;
    if (_loc.Course != null && _loc.Speed > 0.55f) // 2 km/h
      arc = _loc.Course.Value.ToString(CultureInfo.InvariantCulture);
    else if (_compassHeading != null)
      arc = _compassHeading.Value.ToString(CultureInfo.InvariantCulture);
    else
      arc = "null";

    var result = await p_webView.EvaluateJavaScriptAsync($"setCurrentLocation({lat},{lng},{acc},{arc})");
    if (result == null)
      throw new InvalidOperationException($"Can't set current location - js code returned null");
  }

  public async Task SetMapCenterAsync(
    float _lat,
    float _lng,
    int? _zoom = default,
    int? _animationMs = default,
    CancellationToken _ct = default)
  {
    _ct.ThrowIfCancellationRequested();

    var lat = _lat.ToString(CultureInfo.InvariantCulture);
    var lng = _lng.ToString(CultureInfo.InvariantCulture);
    var zoom = _zoom?.ToString(CultureInfo.InvariantCulture) ?? "undefined";
    var animationMs = _animationMs?.ToString(CultureInfo.InvariantCulture) ?? "undefined";

    var result = await p_webView.EvaluateJavaScriptAsync($"setMapCenter({lat},{lng},{zoom},{animationMs})");
    if (result == null)
      throw new InvalidOperationException($"Can't set map location: js code returned null");
  }

  public async Task SetObservedUserAsync(
    string? _user,
    bool _shouldLogInJsConsole,
    CancellationToken _ct)
  {
    _ct.ThrowIfCancellationRequested();

    var user = _user != null ? $"\"{_user}\"" : "null";
    var shouldLogInJsConsole = _shouldLogInJsConsole ? "true" : "false";
    await p_webView.EvaluateJavaScriptAsync($"setObservedUser({user}, {shouldLogInJsConsole})");
  }

  public async Task SetViewToUserOrFallbackToAllTracksAsync(
    string _user, 
    CancellationToken _ct)
  {
    _ct.ThrowIfCancellationRequested();

    var result = await p_webView.EvaluateJavaScriptAsync($"setViewToTrack(\"{_user}\", 13) || setViewToAllTracks();");
    if (result == null)
      throw new InvalidOperationException($"Can't set view to user: js code returned null");
  }

}

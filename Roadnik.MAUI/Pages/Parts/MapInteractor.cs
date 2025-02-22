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

  public async Task<bool> SetMapCenterAsync(
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

    var rawResult = await p_webView.EvaluateJavaScriptAsync($"setMapCenter({lat},{lng},{zoom},{animationMs})");
    if (rawResult == null || !bool.TryParse(rawResult, out var result))
      throw new InvalidOperationException($"Can't set map location: js code returned null");
    
    return result;
  }

  public async Task<bool> SetCurrentLocationAsync(
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

    var rawResult = await p_webView.EvaluateJavaScriptAsync($"setCurrentLocation({lat},{lng},{acc},{arc})");
    if (rawResult == null || !bool.TryParse(rawResult, out var result))
      throw new InvalidOperationException($"Can't set current location - js code returned null");

    return result;
  }

  public async Task<bool> SetMapCenterToUserAsync(
    string _user,
    int? _zoom,
    CancellationToken _ct)
  {
    _ct.ThrowIfCancellationRequested();
    var zoom = _zoom?.ToString(CultureInfo.InvariantCulture) ?? "undefined";

    var rawResult = await p_webView.EvaluateJavaScriptAsync($"setMapCenterToUser(\"{_user}\", {zoom})");
    if (rawResult == null || !bool.TryParse(rawResult, out var result))
      throw new InvalidOperationException($"Can't set view to user: js code returned null");

    return result;
  }

  public async Task<bool> SetMapCenterToAllUsersAsync(
    CancellationToken _ct)
  {
    _ct.ThrowIfCancellationRequested();

    var rawResult = await p_webView.EvaluateJavaScriptAsync($"setMapCenterToAllUsers()");
    if (rawResult == null || !bool.TryParse(rawResult, out var result))
      throw new InvalidOperationException($"Can't set view to user: js code returned null");

    return result;
  }

  public async Task<bool> SetObservedUserAsync(
    string? _user,
    bool _shouldLogInJsConsole,
    CancellationToken _ct)
  {
    _ct.ThrowIfCancellationRequested();

    var user = _user != null ? $"\"{_user}\"" : "null";
    var shouldLogInJsConsole = _shouldLogInJsConsole ? "true" : "false";

    var rawResult = await p_webView.EvaluateJavaScriptAsync($"setObservedUser({user}, {shouldLogInJsConsole})");
    if (rawResult == null || !bool.TryParse(rawResult, out var result))
      throw new InvalidOperationException($"Can't set observed user: js code returned null");

    return result;
  }

}

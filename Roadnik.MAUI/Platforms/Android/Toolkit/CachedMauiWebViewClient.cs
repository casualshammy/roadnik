using Android.Webkit;
using Ax.Fw.SharedTypes.Interfaces;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Roadnik.MAUI.Interfaces;
using System.Text.RegularExpressions;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

public partial class CachedMauiWebViewClient : MauiWebViewClient
{
  private static readonly Regex[] p_cacheRegexes = [
    CacheRegexThunderforest(),
    CacheRegexMapIcon(),
    CacheRegexFavicon(),
    CacheRegexRoomHtml(),
    CacheRegexJs(),
    CacheRegexOsm(),
  ];
  private readonly IMapDataCache? p_tilesCache;
  private readonly IPreferencesStorage? p_storage;
  private readonly ILogger? p_log;

  public CachedMauiWebViewClient(
    WebViewHandler _handler,
    IMapDataCache? _tilesCache,
    ILogger? _log,
    IPreferencesStorage? _storage) : base(_handler)
  {
    p_tilesCache = _tilesCache;
    p_storage = _storage;
    p_log = _log?["cached-web-view-client"];
  }

  public override WebResourceResponse? ShouldInterceptRequest(
    global::Android.Webkit.WebView? _view,
    IWebResourceRequest? _request)
  {
    if (p_tilesCache == null)
      return base.ShouldInterceptRequest(_view, _request);

    var url = _request?.Url?.ToString();
    if (url == null)
      return base.ShouldInterceptRequest(_view, _request);

    try
    {
      var cachedStream = p_tilesCache.Cache.Get(url);
      if (cachedStream != null)
        return new WebResourceResponse(null, null, cachedStream);
    }
    catch (Exception ex)
    {
      p_log?.Error($"Can't get cached resource for url '{url}'", ex);
    }

    if (p_cacheRegexes.All(_ => !_.IsMatch(url)))
      return base.ShouldInterceptRequest(_view, _request);

    p_tilesCache.EnqueueDownload(url);
    return base.ShouldInterceptRequest(_view, _request);
  }

  [GeneratedRegex(@"thunderforest\?type=\w+?&x=\d+&y=\d+&z=\d+$")]
  private static partial Regex CacheRegexThunderforest();
  [GeneratedRegex(@"favicon\.ico$")]
  private static partial Regex CacheRegexFavicon();
  [GeneratedRegex(@"/r/\?id=[\w\-_]+$")]
  private static partial Regex CacheRegexRoomHtml();
  [GeneratedRegex(@"\.js$")]
  private static partial Regex CacheRegexJs();
  [GeneratedRegex(@"openstreetmap\.org/\d+/\d+/\d+\.png$")]
  private static partial Regex CacheRegexOsm();

}
using Android.Webkit;
using HeyRed.Mime;
using JustLogger.Interfaces;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Roadnik.MAUI.Interfaces;
using System.Text.RegularExpressions;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Platforms.Android.Toolkit;

public class CachedMauiWebViewClient : MauiWebViewClient
{
  private readonly Regex[] p_cacheRegexes = {
    new Regex(@"thunderforest\?type=\w+?&x=\d+&y=\d+&z=\d+$", RegexOptions.Compiled),
    new Regex(@"img/map_icon_\d+\.png$", RegexOptions.Compiled),
    new Regex(@"favicon\.ico$", RegexOptions.Compiled),
    new Regex(@"\?roomId=[\w\-_]+$", RegexOptions.Compiled),
    new Regex(@"\.js$", RegexOptions.Compiled),
    new Regex(@"openstreetmap\.org/\d+/\d+/\d+\.png$", RegexOptions.Compiled),
  };
  private readonly ITilesCache? p_tilesCache;
  private readonly IPreferencesStorage? p_storage;
  private readonly ILogger? p_log;

  public CachedMauiWebViewClient(
    WebViewHandler _handler, 
    ITilesCache? _tilesCache,
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

    var cacheEnabled = p_storage?.GetValueOrDefault<bool>(PREF_MAP_CACHE_ENABLED);
    if (cacheEnabled != true)
      return base.ShouldInterceptRequest(_view, _request);

    var url = _request?.Url?.ToString();
    if (url == null)
      return base.ShouldInterceptRequest(_view, _request);

    try
    {
      var cachedStream = p_tilesCache.Cache.Get(url);
      if (cachedStream != null)
      {
        var mime = MimeTypesMap.GetMimeType(url);
        return new WebResourceResponse(mime, null, cachedStream);
      }
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
}